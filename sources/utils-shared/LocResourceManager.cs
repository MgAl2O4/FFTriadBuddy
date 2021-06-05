using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;

namespace MgAl2O4.Utils
{
    public class LocResourceManager
    {
        private static LocResourceManager instance = new LocResourceManager();
        private Dictionary<string, string> strings = new Dictionary<string, string>();

        public static LocResourceManager Get()
        {
            return instance;
        }

        public string FindString(string key, bool defaultToNull = false)
        {
            string resultStr = null;
            if (!strings.TryGetValue(key, out resultStr))
            {
                if (!defaultToNull)
                {
                    resultStr = string.Format("--LOC:{0}--", key);
                }
            }

            return resultStr;
        }

        public static void SetCurrentUserLanguage(CultureInfo cultureInfo, Type stringContainerType)
        {
            var mapCultureAssets = new Dictionary<string, string>();
            var listAssets = AssetManager.Get().ListAssets();

            foreach (var assetPath in listAssets)
            {
                if (assetPath.EndsWith(".resx"))
                {
                    string fileName = Path.GetFileName(assetPath).Replace(".resx", "");
                    int sepPos = fileName.IndexOf('.');
                    if (sepPos > 0)
                    {
                        string locCode = fileName.Substring(sepPos + 1);
                        mapCultureAssets.Add(locCode, assetPath);
                    }
                    else
                    {
                        // default to neutral culture resx
                        mapCultureAssets.Add("", assetPath);
                    }
                }
            }

            var loadList = new List<string>();
            while (cultureInfo != null && cultureInfo != CultureInfo.InvariantCulture && cultureInfo.Name.Length > 0)
            {
                if (mapCultureAssets.TryGetValue(cultureInfo.Name, out string assetPath))
                {
                    Logger.WriteLine("Found localization resource for: {0}", cultureInfo.Name);
                    loadList.Add(assetPath);
                }

                cultureInfo = cultureInfo.Parent;
            }

            // always include neutral culture for fallback
            loadList.Add(mapCultureAssets[""]);

            Get().LoadResourceHierarchy(loadList);
            Get().CopyToStringContainer(stringContainerType);
        }

        public void LoadResourceHierarchy(List<string> paths)
        {
            strings.Clear();
            foreach (var path in paths)
            {
                LoadResource(path);
            }
        }

        public void LoadResource(string path)
        {
            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(AssetManager.Get().GetAsset(path));

                foreach (XmlNode testNode in xdoc.DocumentElement.ChildNodes)
                {
                    XmlElement testElem = testNode as XmlElement;
                    if (testElem != null && testElem.Name == "data")
                    {
                        try
                        {
                            string keyStr = testElem.GetAttribute("name");
                            if (!strings.ContainsKey(keyStr))
                            {
                                foreach (XmlNode innerNode in testElem.ChildNodes)
                                {
                                    XmlElement valueElem = innerNode as XmlElement;
                                    if (valueElem != null)
                                    {
                                        strings.Add(keyStr, valueElem.InnerText);
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("Loading failed! Exception:" + ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Loading failed! Exception:" + ex);
            }
        }

        public void CopyToStringContainer(Type stringContainerType)
        {
            var allFields = stringContainerType.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in allFields)
            {
                if (field.FieldType == typeof(string))
                {
                    field.SetValue(null, FindString(field.Name));
                }
            }
        }
    }
}
