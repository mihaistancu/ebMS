using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CommandLine;
using MimeKit;

namespace ebMS
{
    class Program
    {
        private static Uri uri;
        private static XmlDocument soap;
        private static string sedPath;
        private static X509Certificate2 certificate;

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
            {
                uri = new Uri(o.Url);
                soap = new XmlDocument{PreserveWhitespace = true};
                certificate = new X509Certificate2(o.TlsCertificate);
                soap.Load(o.Soap);
                sedPath = o.Sed;
                
                Send();
            });
        }

        public static void Send()
        {
            var mimeMessage = Serialize();

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
            request.ContentType = mimeMessage.ContentType.MimeType + mimeMessage.ContentType.Parameters;
            request.ClientCertificates.Add(certificate);
            
            using (Stream requestStream = request.GetRequestStream())
            {
                mimeMessage.WriteTo(requestStream, true);
            }
    
            var response = (HttpWebResponse)request.GetResponse();
            
            using (Stream responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream))
            {
                Console.WriteLine(reader.ReadToEnd());
            }
        }

        private static readonly Dictionary<string, string> MimeMap = new Dictionary<string, string>
        {
            {".xml", "application/xml"},
            {".gz", "application/gzip"}
        };

        public static MimeEntity Serialize()
        {
            var stream = new MemoryStream();
            soap.Save(stream);

            var mimeRoot = new MimePart("application", "soap+xml")
            {
                Content = new MimeContent(stream)
            };

            var multipart = new Multipart("related") {mimeRoot};

            var contentType = MimeMap[Path.GetExtension(sedPath)];

            var mimeAttachment = new MimePart(contentType)
            {
                ContentId = "DefaultSED",
                Content = new MimeContent(File.OpenRead(sedPath)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Binary
            };
            multipart.Add(mimeAttachment);

            return multipart;
        }
    }

    public class Options
    {
        [Option]
        public string Url { get; set; }

        [Option]
        public string TlsCertificate { get; set; }

        [Option]
        public string Soap { get; set; }

        [Option]
        public string Sed { get; set; }
    }
}
