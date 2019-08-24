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
        Cactpot,
    }

    public class HashCollection
    {
        public readonly TlshHash ComplexHash;
        public readonly ScanLineHash SimpleHash;
        
        public HashCollection(TlshHash complexHash, ScanLineHash simpleHash)
        {
            ComplexHash = complexHash;
            SimpleHash = simpleHash;
        }

        public HashCollection(string complexHashStr, string simpleHashStr)
        {
            ComplexHash = string.IsNullOrEmpty(complexHashStr) ? null : TlshHash.FromTlshStr(complexHashStr);
            SimpleHash = string.IsNullOrEmpty(simpleHashStr) ? null : ScanLineHash.FromString(simpleHashStr);
        }

        public bool IsMatching(HashCollection other, out int distance)
        {
            distance = FindDistance(other);
            int maxMatchDistance = (ComplexHash != null) ? 19 : 0;
            return distance <= maxMatchDistance;
        }

        public int FindDistance(HashCollection other)
        {
            if (ComplexHash != null && other.ComplexHash != null)
            {
                return ComplexHash.TotalDiff(other.ComplexHash, false);
            }

            if (SimpleHash != null && other.SimpleHash != null)
            {
                return SimpleHash.GetDistance(other.SimpleHash);
            }

            return int.MaxValue;
        }

        public override string ToString()
        {
            return (ComplexHash != null) ? ("C:" + ComplexHash) : ("S:" + SimpleHash);
        }
    }

    public class ImageHashData : IComparable
    {
        public readonly object Owner;
        public readonly EImageHashType Type;
        public readonly HashCollection Hash;
        public object GuideOb;

        public ImageHashData(object owner, HashCollection hash, EImageHashType type)
        {
            Owner = owner;
            Hash = hash;
            Type = type;
        }

        public ImageHashData(object owner, HashCollection hash, EImageHashType type, object guideOb)
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

        public bool IsHashMatching(HashCollection testHash)
        {
            int dummyDistance = 0;
            return Hash.IsMatching(testHash, out dummyDistance);
        }

        public bool IsHashMatching(HashCollection testHash, out int distance)
        {
            return Hash.IsMatching(testHash, out distance);
        }

        public bool IsHashMatching(ImageHashData testHashData)
        {
            int dummyDistance = 0;
            return Hash.IsMatching(testHashData.Hash, out dummyDistance);
        }

        public bool IsHashMatching(ImageHashData testHashData, out int distance)
        {
            return Hash.IsMatching(testHashData.Hash, out distance);
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
            CactpotGame.InititalizeHashDB();

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
            if (xmlElem != null && xmlElem.Name == "hash" && xmlElem.HasAttribute("type") && (xmlElem.HasAttribute("value") || xmlElem.HasAttribute("valueS")))
            {
                string typeName = xmlElem.GetAttribute("type");

                string hashValueC = xmlElem.HasAttribute("value") ? xmlElem.GetAttribute("value") : null;
                string hashValueS = xmlElem.HasAttribute("valueS") ? xmlElem.GetAttribute("valueS") : null;

                HashCollection hashData = new HashCollection(hashValueC, hashValueS);

                if (typeName.Equals("rule", StringComparison.InvariantCultureIgnoreCase))
                {
                    string ruleName = xmlElem.GetAttribute("name");
                    TriadGameModifier ruleMod = ParseRule(ruleName);

                    result = new ImageHashData(ruleMod, hashData, EImageHashType.Rule);
                }
                else if (typeName.Equals("card", StringComparison.InvariantCultureIgnoreCase))
                {
                    string cardIdName = xmlElem.GetAttribute("id");
                    int cardId = int.Parse(cardIdName);
                    TriadCard cardOb = TriadCardDB.Get().cards[cardId];

                    result = new ImageHashData(cardOb, hashData, EImageHashType.Card);
                }
                else if (typeName.Equals("cactpot", StringComparison.InvariantCultureIgnoreCase))
                {
                    string numIdName = xmlElem.GetAttribute("id");
                    int numId = int.Parse(numIdName);
                    if (numId >= 1 && numId <= 9)
                    {
                        result = new ImageHashData(CactpotGame.hashDB[numId - 1], hashData, EImageHashType.Cactpot);
                    }
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

            // store only one of hashes
            if (entry.Hash.ComplexHash != null)
            {
                xmlWriter.WriteAttributeString("value", entry.Hash.ComplexHash.ToString());
            }
            else if (entry.Hash.SimpleHash != null)
            {
                xmlWriter.WriteAttributeString("valueS", entry.Hash.SimpleHash.ToString());
            }

            switch (entry.Type)
            {
                case EImageHashType.Rule: xmlWriter.WriteAttributeString("name", entry.Owner.ToString()); break;
                case EImageHashType.Card: xmlWriter.WriteAttributeString("id", ((TriadCard)entry.Owner).Id.ToString()); break;
                case EImageHashType.Cactpot: xmlWriter.WriteAttributeString("id", entry.Owner.ToString()); break;
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
