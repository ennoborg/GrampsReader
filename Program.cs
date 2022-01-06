using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace GrampsReader
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Need filename!");
                return;
            }

            var filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.WriteLine("File not found: " + filePath);
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
                        var id = person.Attribute("id").Value;
                        // Console.WriteLine($"Person: {id}");
                        // Een persoon kan verscheidene namen hebben.
                        foreach (XElement name in person.Elements(gr + "name"))
                        {
                            var first = name.Element(gr + "first")?.Value;
                            var last = name.Element(gr + "surname")?.Value;
                            // Console.WriteLine($"    {first} {last}");

                            if (first != null)
                            {
                                // Zoek naar van, de, den, der in de voornaam.
                                char[] splitChars = { ' ' };

                                var firstParts = first.Split(splitChars, System.StringSplitOptions.RemoveEmptyEntries);

                                string prefix = "";
                                string given = "";

                                bool warning = false;

                                foreach (string word in firstParts)
                                {
                                    var w = word.Trim();

                                    switch (w)
                                    {
                                        case "de":
                                        case "den":
                                        case "der":
                                        case "la":
                                        case "op":
                                        case "'t":
                                        case "ten":
                                        case "ter":
                                        case "van":
                                            if (prefix.Length > 0)
                                            {
                                                prefix += " ";
                                            }
                                            prefix += w;
                                            break;

                                        default:
                                            // Als er nog een gewone naam achter een prefix komt,
                                            // kan de conversie niet helemaal goed gaan.
                                            if (prefix.Length > 0)
                                            {
                                                warning = true;
                                            }
                                            if (given.Length > 0)
                                            {
                                                given += " ";
                                            }
                                            given += w;
                                            break;
                                    }
                                }

                                // Als prefix niet leeg is, moet die voor de achternaam worden toegevoegd.
                                if (warning)
                                {
                                    Console.WriteLine($"WARNING: Can't convert {id} : {first} /{last}/");
                                }
                                else
                                if (prefix.Length > 0)
                                {
                                    var surname = prefix + " " + last;

                                    Console.WriteLine($"Nieuwe naam {id} : {given} /{surname}/");

                                    // Plaats given in first, surname in surname.
                                    name.Element(gr + "first").Value = given;

                                    // De oude surname kan null zijn.
                                    if (last != null)
                                    {
                                        name.Element(gr + "surname").Value = surname;
                                    }
                                    else
                                    {
                                        name.Add(new XElement(gr + "surname", surname));
                                    }
                                }
                            }
                        }
                    }

                    // Trim alle dubbele komma's uit plaatsnamen.
                    int plaatsen = 0;

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
                            plaatsen++;
                        }
                    }

                    var sources = doc.Element(gr + "database").Element(gr + "sources").Elements(gr + "source");
                    int bronnen = 0;
                    int afkos = 0;

                    var sourceList = new List<XElement>();

                    foreach (XElement cell in sources)
                    {
                        sourceList.Add(cell);

                        string title = cell.Element(gr + "stitle").Value;
                        var abbrev = cell.Element(gr + "sabbrev");

                        if (abbrev != null && abbrev.Value == title)
                        {
                            abbrev.Remove();
                            afkos++;
                        }

                        string clean = title.Replace(";", "");

                        if (clean != title)
                        {
                            // Titel met ; erin.
                            // Kijk of deze ook bestaat.
                            foreach (var zoek in sources)
                            {
                                var zoekTitel = zoek.Element(gr + "stitle").Value;

                                if (zoekTitel == clean)
                                {
                                    // Gevonden!
                                    Console.WriteLine("Dubbele titel: " + clean);
                                    cell.Element(gr + "stitle").Value = clean;

                                    bronnen++;
                                }
                            }
                        }
                    }

                    var duplicateTitles = new List<string>();

                    for (var i = 0; i < sourceList.Count; i++)
                    {
                        for (var j = i + 1; j < sourceList.Count; j++)
                        {
                            var t1 = sourceList[i].Element(gr + "stitle").Value;
                            var t2 = sourceList[j].Element(gr + "stitle").Value;

                            var a1 = sourceList[i].Element(gr + "sauthor")?.Value;
                            var a2 = sourceList[j].Element(gr + "sauthor")?.Value;

                            if (CleanText(t1) == CleanText(t2) && a1 == a2)
                            {
                                var i1 = sourceList[i].Attribute("id").Value;
                                var i2 = sourceList[j].Attribute("id").Value;

                                var dup = "\n" + t1 + " : " + i1 + "\n" + t2 + " : " + i2;

                                duplicateTitles.Add(dup);
                            }
                        }
                    }

                    duplicateTitles.Sort();

                    if (plaatsen > 0)
                    {
                        Console.WriteLine("Aantal aangepaste plaatsen = " + plaatsen);
                    }

                    if (bronnen > 0)
                    {
                        Console.WriteLine("Aantal aangepaste bronnen = " + bronnen);
                    }

                    if (afkos > 0)
                    {
                        Console.WriteLine("Aantal redundante/verwijderde afkos = " + afkos);
                    }

                    Console.WriteLine("Aantal dubbele bronnen = " + duplicateTitles.Count);
                    
                    foreach (var dup in duplicateTitles)
                    {
                        Console.WriteLine(dup);
                    }
 
                    // Save new XML
                    doc.Save(@"new-tree.gramps");
                }
            }
        }

        private static string CleanText(string t)
        {
            // return t.Replace(" ", "").Replace(":", "").Replace(".", "").Replace(";", "");
            var s = string.Empty;

            foreach (var c in t)
            {
                if (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9')
                {
                    s += c;
                }
            }

            return s.ToUpper();
        }
    }
}
