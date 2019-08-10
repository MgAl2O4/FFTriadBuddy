using Palit.TLSHSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FFTriadBuddy
{
    public enum EImageHashType
    {
        None,
        Rule,
        Card,
    }

    public class ImageHashData : IComparable
    {
        public readonly object Owner;
        public readonly TlshHash Hash;
        public readonly EImageHashType Type;
        public object GuideOb;

        public ImageHashData(object owner, TlshHash hash, EImageHashType type)
        {
            Owner = owner;
            Hash = hash;
            Type = type;
        }

        public ImageHashData(object owner, TlshHash hash, EImageHashType type, object guideOb)
        {
            Owner = owner;
            Hash = hash;
            Type = type;
            GuideOb = guideOb;
        }

        public int CompareTo(object obj)
        {
            ImageHashData otherOb = (ImageHashData)obj;
            return (otherOb == null) ? 1 :
                (Type != otherOb.Type) ? Type.CompareTo(otherOb.Type) :
                Hash.ToString().CompareTo(otherOb.Hash.ToString());
        }

        public int GetDistance(TlshHash testHash)
        {
            return (Hash != null && testHash != null) ? Hash.TotalDiff(testHash, false) : int.MaxValue;
        }

        public int GetDistance(ImageHashData testHash)
        {
            return GetDistance(testHash.Hash);
        }

        public override string ToString()
        {
            return Type + ": " + Owner;
        }
    }

    public class ImageHashDB
    {
        public List<ImageHashData> hashes;
        public string DBPath;
        private static ImageHashDB instance = new ImageHashDB();

        public List<TriadGameModifier> modObjects;

        public ImageHashDB()
        {
            DBPath = "data/hashes.xml";
            hashes = new List<ImageHashData>();
            modObjects = new List<TriadGameModifier>();
        }

        public static ImageHashDB Get()
        {
            return instance;
        }

        public bool Load()
        {
            hashes.Clear();

            modObjects.Clear();
            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)))
                {
                    modObjects.Add((TriadGameModifier)Activator.CreateInstance(type));
                }
            }

            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(AssetManager.Get().GetAsset(DBPath));

                foreach (XmlNode testNode in xdoc.DocumentElement.ChildNodes)
                {
                    XmlElement testElem = (XmlElement)testNode;
                    ImageHashData hashEntry = LoadHashEntry(testElem);
                    if (hashEntry != null)
                    {
                        hashes.Add(hashEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Loading failed! Exception:" + ex);
            }

            Logger.WriteLine("Loaded hashes: " + hashes.Count);
            return true;
        }

        public ImageHashData LoadHashEntry(XmlElement xmlElem)
        {
            ImageHashData result = null;
            if (xmlElem != null && xmlElem.Name == "hash" && xmlElem.HasAttribute("type") && xmlElem.HasAttribute("value"))
            {
                string typeName = xmlElem.GetAttribute("type");
                string hashValue = xmlElem.GetAttribute("value");

                if (typeName.Equals("rule", StringComparison.InvariantCultureIgnoreCase))
                {
                    string ruleName = xmlElem.GetAttribute("name");
                    TriadGameModifier ruleMod = ParseRule(ruleName);

                    result = new ImageHashData(ruleMod, TlshHash.FromTlshStr(hashValue), EImageHashType.Rule);
                }
                else if (typeName.Equals("card", StringComparison.InvariantCultureIgnoreCase))
                {
                    string cardIdName = xmlElem.GetAttribute("id");
                    int cardId = int.Parse(cardIdName);
                    TriadCard cardOb = TriadCardDB.Get().cards[cardId];

                    result = new ImageHashData(cardOb, TlshHash.FromTlshStr(hashValue), EImageHashType.Card);
                }
            }

            return result;
        }

        public ImagePatternDigit LoadDigitEntry(XmlElement xmlElem)
        {
            ImagePatternDigit result = new ImagePatternDigit(-1, null);

            if (xmlElem != null && xmlElem.Name == "digit" && xmlElem.HasAttribute("type") && xmlElem.HasAttribute("value"))
            {
                string typeName = xmlElem.GetAttribute("type");
                string hashValue = xmlElem.GetAttribute("value");

                result = new ImagePatternDigit(int.Parse(typeName), ImageDataDigit.FromHexString(hashValue));
            }

            return result;
        }

        public void StoreEntry(ImagePatternDigit entry, XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("digit");
            xmlWriter.WriteAttributeString("type", entry.Value.ToString());
            xmlWriter.WriteAttributeString("value", entry.Hash);
            xmlWriter.WriteEndElement();
        }

        public void StoreEntry(ImageHashData entry, XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("hash");
            xmlWriter.WriteAttributeString("type", entry.Type.ToString());
            xmlWriter.WriteAttributeString("value", entry.Hash.ToString());

            switch (entry.Type)
            {
                case EImageHashType.Rule: xmlWriter.WriteAttributeString("name", entry.Owner.ToString()); break;
                case EImageHashType.Card: xmlWriter.WriteAttributeString("id", ((TriadCard)entry.Owner).Id.ToString()); break;
                default: break;
            }

            xmlWriter.WriteEndElement();
        }

        private TriadGameModifier ParseRule(string ruleName)
        {
            TriadGameModifier result = null;
            foreach (TriadGameModifier mod in modObjects)
            {
                if (ruleName.Equals(mod.GetName(), StringComparison.InvariantCultureIgnoreCase))
                {
                    result = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                    break;
                }
            }

            if (result == null)
            {
                Logger.WriteLine("Loading failed! Can't parse rule: " + ruleName);
            }

            return result;
        }
    }
}
