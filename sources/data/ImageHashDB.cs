using MgAl2O4.Utils;
using Palit.TLSHSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

namespace FFTriadBuddy
{
    public enum EImageHashType
    {
        CardNumber,
        CardImage,
        Rule,
        Cactpot,
    }

    public class ImageHashData : IComparable
    {
        public byte[] hashMD5;
        public TlshHash hashTLSH;
        public EImageHashType type;

        public object ownerOb;

        public bool isAuto;
        public bool isKnown;

        public int matchDistance;
        public Image previewImage;
        public Bitmap sourceImage;
        public Rectangle previewBounds;
        public Rectangle previewContextBounds;

        public void CalculateHash(byte[] data)
        {
            TlshBuilder hashBuilder = new TlshBuilder();
            hashBuilder.Update(data);
            hashTLSH = hashBuilder.IsValid(false) ? hashBuilder.GetHash(false) : null;

            using (MD5 md5Builder = MD5.Create())
            {
                hashMD5 = md5Builder.ComputeHash(data);
            }
        }

        public void CalculateHash(float[] data)
        {
            byte[] byteData = new byte[data.Length * sizeof(float)];
            Buffer.BlockCopy(data, 0, byteData, 0, byteData.Length);
            CalculateHash(byteData);
        }

        private static int GetHexVal(char hex)
        {
            return (hex >= 'a' && hex <= 'f') ? (hex - 'a' + 10) :
                (hex >= 'A' && hex <= 'F') ? (hex - 'A' + 10) :
                hex;
        }

        public void LoadFromString(string descTLSH, string descBuffer)
        {
            if (!string.IsNullOrEmpty(descTLSH))
            {
                hashTLSH = TlshHash.FromTlshStr(descTLSH);
            }

            if (!string.IsNullOrEmpty(descBuffer))
            {
                hashMD5 = new byte[descBuffer.Length / 2];
                for (int idx = 0; idx < descBuffer.Length; idx++)
                {
                    hashMD5[idx / 2] = (byte)((GetHexVal(descBuffer[idx]) << 4) + GetHexVal(descBuffer[idx]));
                }
            }
        }

        public bool IsMatching(ImageHashData other, int maxDistance, out int matchDistance)
        {
            matchDistance = GetHashDistance(other);
            return matchDistance <= maxDistance;
        }

        public int GetHashDistance(ImageHashData other)
        {
            if (hashMD5 != null && other.hashMD5 != null && hashMD5.Length == other.hashMD5.Length)
            {
                bool isMatching = true;
                for (int idx = 0; idx < hashMD5.Length; idx++)
                {
                    if (hashMD5[idx] != other.hashMD5[idx])
                    {
                        isMatching = false;
                        break;
                    }
                }

                if (isMatching)
                {
                    return 0;
                }
            }

            if (hashTLSH != null && other.hashTLSH != null)
            {
                return hashTLSH.TotalDiff(other.hashTLSH, false);
            }

            return int.MaxValue;
        }

        public void UpdatePreviewImage()
        {
            if (previewImage == null)
            {
                previewImage = ImageUtils.CreatePreviewImage(sourceImage, previewBounds, previewContextBounds);
            }
        }

        public int CompareTo(object obj)
        {
            ImageHashData otherOb = (ImageHashData)obj;
            if (otherOb == null) { return 1; }
            if (type != otherOb.type) { return type.CompareTo(otherOb.type); }

            return ownerOb.ToString().CompareTo(otherOb.ownerOb.ToString());
        }

        public bool IsValid()
        {
            return (hashMD5 != null) || (hashTLSH != null);
        }

        public override string ToString()
        {
            return type + ": " + ownerOb;
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
                    if (hashEntry != null && hashEntry.IsValid())
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
            if (xmlElem != null && xmlElem.Name == "hash" && xmlElem.HasAttribute("type") && (xmlElem.HasAttribute("value") || xmlElem.HasAttribute("valueB")))
            {
                string typeName = xmlElem.GetAttribute("type");

                string hashValueC = xmlElem.HasAttribute("value") ? xmlElem.GetAttribute("value") : null;
                string hashValueB = xmlElem.HasAttribute("valueB") ? xmlElem.GetAttribute("valueB") : null;

                if (typeName.Equals("rule", StringComparison.InvariantCultureIgnoreCase))
                {
                    string ruleName = xmlElem.GetAttribute("name");

                    result = new ImageHashData() { type = EImageHashType.Rule, isKnown = true };
                    result.ownerOb = ParseRule(ruleName);
                    result.LoadFromString(hashValueC, hashValueB);
                }
                else if (typeName.Equals("card", StringComparison.InvariantCultureIgnoreCase))
                {
                    string cardIdName = xmlElem.GetAttribute("id");
                    int cardId = int.Parse(cardIdName);

                    result = new ImageHashData() { type = EImageHashType.CardImage, isKnown = true };
                    result.ownerOb = TriadCardDB.Get().cards[cardId];
                    result.LoadFromString(hashValueC, hashValueB);
                }
                else if (typeName.Equals("cactpot", StringComparison.InvariantCultureIgnoreCase))
                {
                    string numIdName = xmlElem.GetAttribute("id");

                    result = new ImageHashData() { type = EImageHashType.Cactpot, isKnown = true };
                    result.ownerOb = int.Parse(numIdName);
                    result.LoadFromString(hashValueC, hashValueB);
                }
            }

            return result;
        }

        public List<ImageHashData> LoadImageHashes(JsonParser.ObjectValue jsonOb)
        {
            List<ImageHashData> list = new List<ImageHashData>();

            string[] enumArr = Enum.GetNames(typeof(EImageHashType));
            foreach (var kvp in jsonOb.entries)
            {
                EImageHashType groupType = (EImageHashType)Array.IndexOf(enumArr, kvp.Key);
                JsonParser.ArrayValue typeArr = (JsonParser.ArrayValue)kvp.Value;

                foreach (JsonParser.Value value in typeArr.entries)
                {
                    JsonParser.ObjectValue jsonHashOb = (JsonParser.ObjectValue)value;
                    string idStr = jsonHashOb["id"];

                    bool hasIdNum = int.TryParse(idStr, out int idNum);
                    bool needsIdNum = (groupType != EImageHashType.Rule);
                    if (hasIdNum != needsIdNum)
                    {
                        continue;
                    }

                    ImageHashData hashEntry = new ImageHashData() { type = groupType, isKnown = true };
                    switch (groupType)
                    {
                        case EImageHashType.Rule:
                            hashEntry.ownerOb = ParseRule(idStr);
                            break;

                        case EImageHashType.CardImage:
                            hashEntry.ownerOb = TriadCardDB.Get().cards[idNum];
                            break;

                        default:
                            hashEntry.ownerOb = idNum;
                            break;
                    }

                    if (hashEntry.ownerOb != null)
                    {
                        string descHashTLSH = jsonHashOb["hashC", JsonParser.StringValue.Empty];
                        string descHashMd5 = jsonHashOb["hashB", JsonParser.StringValue.Empty];

                        hashEntry.LoadFromString(descHashTLSH, descHashMd5);
                        if (hashEntry.IsValid())
                        {
                            list.Add(hashEntry);
                        }
                    }
                }
            }

            return list;
        }

        public void StoreHashes(List<ImageHashData> entries, JsonWriter jsonWriter)
        {
            foreach (EImageHashType subType in Enum.GetValues(typeof(EImageHashType)))
            {
                List<ImageHashData> sortedSubtypeList = entries.FindAll(x => x.type == subType);
                sortedSubtypeList.Sort();

                jsonWriter.WriteArrayStart(subType.ToString());
                foreach (ImageHashData entry in sortedSubtypeList)
                {
                    jsonWriter.WriteObjectStart();
                    switch (subType)
                    {
                        case EImageHashType.CardImage: jsonWriter.WriteString(((TriadCard)entry.ownerOb).Id.ToString(), "id"); break;
                        default: jsonWriter.WriteString(entry.ownerOb.ToString(), "id"); break;
                    }

                    if (entry.hashTLSH != null)
                    {
                        jsonWriter.WriteString(entry.hashTLSH.ToString(), "hashC");
                    }
                    else
                    {
                        string hexStr = BitConverter.ToString(entry.hashMD5).ToLower();
                        jsonWriter.WriteString(hexStr, "hashB");
                    }

                    jsonWriter.WriteObjectEnd();
                }
                jsonWriter.WriteArrayEnd();
            }
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

        public ImageHashData FindExactMatch(ImageHashData hashData)
        {
            return FindBestMatch(hashData, 0, out int dummyV);
        }

        public ImageHashData FindBestMatch(ImageHashData hashData, int maxDistance, out int matchDistance)
        {
            int bestDistance = 0;
            int bestIdx = -1;

            for (int idx = 0; idx < hashes.Count; idx++)
            {
                if (hashes[idx].type == hashData.type)
                {
                    int distance = hashes[idx].GetHashDistance(hashData);
                    if (distance <= maxDistance)
                    {
                        if (bestIdx < 0 || bestDistance > distance)
                        {
                            bestIdx = idx;
                            bestDistance = distance;
                        }
                    }
                }
            }

            matchDistance = bestDistance;
            return (bestIdx < 0) ? null : hashes[bestIdx];
        }
    }
}
