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
            var filePath = args[0];

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
                    XNamespace gr = "http://gramps-project.org/xml/1.5.1/";

                    XDocument doc = XDocument.Load(reader);

                    // Zoek alle nodes van het type phone.
                    var test = doc.Descendants(gr + "phone");

                    foreach (var num in test)
                    {
                        Console.WriteLine("Telefoon: " + num.Value);
                    }

                    // Lees alle personen.
                    foreach (XElement person in doc.Element(gr + "database").Element(gr + "people").Elements(gr + "person"))
                    {
                        var name = person.Element(gr + "name");
                        var first = name.Element(gr + "first")?.Value;
                        var last = name.Element(gr + "surname")?.Value;
                        Console.WriteLine($"Person: {first} {last}");
                    }

                    // Trim alle dubbele komma's uit plaatsnamen.
                    foreach (XElement cell in doc.Element(gr + "database").Element(gr + "places").Elements(gr + "placeobj"))
                    {
                        string plaats = cell.Element(gr + "ptitle").Value;

                        // Hier kunnen zaken als meervoudige komma's en andere fouten worden opgelost.
                        char[] trimChars = { ',' };

                        var namen = plaats.Trim(trimChars).Split(trimChars, System.StringSplitOptions.RemoveEmptyEntries);

                        string nieuw = "";

                        foreach (string naam in namen)
                        {
                            var w = naam.Trim();
                            // We kunnen hier ook namen vertalen
                            switch (w)
                            {
                                case "Eng.":
                                case "Engeland":
                                    w = "England";
                                    break;

                                case "Germany":
                                    w = "Deutschland";
                                    break;

                                case "Preussen":
                                case "Prussia":
                                    w = "Preußen";
                                    break;

                                case "Netherland":
                                case "Netherlands":
                                case "NL":
                                case "The Netherlands":
                                    w = "Nederland";
                                    break;

                                case "Gldrl":
                                    w = "Gelderland";
                                    break;

                                case "Holland (North)":
                                case "North Holland":
                                    w = "Noord-Holland";
                                    break;

                                case "Overijsel":
                                    w = "Overijssel";
                                    break;

                                case "South Holland":
                                case "S Hlln":
                                case "S-Hlln":
                                case "Holland (South)":
                                case "Zuid Holland":
                                    w = "Zuid-Holland";
                                    break;
                            }

                            if (w.Length > 0)
                            {
                                if (nieuw.Length > 0)
                                {
                                    nieuw = nieuw + ", ";
                                }
                                nieuw = nieuw + w;
                            }
                        }

                        if (plaats != nieuw)
                        {
                            Console.WriteLine(plaats + " -> " + nieuw);

                            // Schrijf nieuwe plaats terug in doc:
                            cell.Element(gr + "ptitle").Value = nieuw;
                        }
                    }

                    // Save new XML
                    doc.Save(@"..\..\new-tree.gramps");
                }
            }
        }
    }
}
