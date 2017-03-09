using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace GrampsReader
{
    class Program
    {
        static void Main(string[] args)
        {
            var filePath = @"..\..\..\data.gramps";

            if (!File.Exists(filePath))
            {
                Debug.WriteLine("File not found: " + filePath);
                return;
            }

            using (Stream fileStream = File.OpenRead(filePath),
              zippedStream = new GZipStream(fileStream, CompressionMode.Decompress))
            {
                using (StreamReader reader = new StreamReader(zippedStream))
                {
                    XElement grampsTree = XElement.Load(reader);

                    XNamespace gr = "http://gramps-project.org/xml/1.5.1/";

                    // Zoek alle nodes van het type phone.
                    var test = grampsTree.Descendants(gr + "phone");

                    foreach (var num in test)
                    {
                        Debug.WriteLine("Telefoon: " + num.Value);
                    }
                }
            }
        }
    }
}
