using System;
using System.Collections.Generic;
using System.Text;

namespace MgAl2O4.Utils
{
    // really trying to avoid 3rd party dependency dll files...
    // api replies are simple enough and always valid

    public class JsonParser
    {
        public abstract class Value
        {
            public override string ToString() { return ""; }
            public virtual string ToExportString() { return ToString(); }
            public static implicit operator string(Value v) => v.ToString();
        }

        public class NullValue : Value
        {
            public NullValue() { }
            public override string ToString() { return "null"; }
        }

        public class BoolValue : Value
        {
            public bool bFlag;

            public BoolValue(bool bFlag) { this.bFlag = bFlag; }
            public override string ToString() { return bFlag ? "true" : "false"; }
            public static implicit operator bool(BoolValue v) => v.bFlag;
            public static BoolValue Empty = new BoolValue(false);
        }

        public class IntValue : Value
        {
            public int Number;

            public IntValue(int Num) { Number = Num; }
            public override string ToString() { return Number.ToString(); }
            public static implicit operator int(IntValue v) => v.Number;
            public static IntValue Empty = new IntValue(0);
        }

        public class FloatValue : Value
        {
            public float Number;

            public FloatValue(float Num) { Number = Num; }
            public override string ToString() { return Number.ToString(); }
            public static implicit operator float(FloatValue v) => v.Number;
            public static FloatValue Empty = new FloatValue(0);
        }

        public class StringValue : Value
        {
            public string Str = "";

            public StringValue(string Str) { this.Str = Str; }
            public override string ToString() { return Str; }
            public override string ToExportString() { return "\"" + Str + "\""; }
            public static implicit operator string(StringValue v) => (v != null) ? v.Str : null;
            public static StringValue Empty = new StringValue("");
        }

        public abstract class ContainerValue : Value
        {
            public abstract void Add(string key, Value value);
        }

        public class ObjectValue : ContainerValue
        {
            public Dictionary<string, Value> entries = new Dictionary<string, Value>();

            public override void Add(string key, Value value)
            {
                entries.Add(key, value);
            }

            public override string ToString()
            {
                string desc = "{";
                foreach (KeyValuePair<string, Value> kvp in entries)
                {
                    desc += "\"" + kvp.Key + "\":" + kvp.Value + ", ";
                }

                if (desc.Length > 1)
                {
                    desc = desc.Remove(desc.Length - 2, 2);
                    desc += "}";
                }

                return desc;
            }

            public Value this[string key] { get => entries[key]; }
            public Value this[string key, Value defaultValue] { get => entries.ContainsKey(key) ? entries[key] : defaultValue; }
            public static ObjectValue Empty = new ObjectValue();
        }

        public class ArrayValue : ContainerValue
        {
            public List<Value> entries = new List<Value>();

            public override void Add(string key, Value value)
            {
                entries.Add(value);
            }

            public override string ToString()
            {
                string desc = "[";
                foreach (Value item in entries)
                {
                    desc += item + ", ";
                }

                if (desc.Length > 1)
                {
                    desc = desc.Remove(desc.Length - 2, 2);
                    desc += "]";
                }

                return desc;
            }

            public Value this[int key] { get => entries[key]; }
            public static ArrayValue Empty = new ArrayValue();
        }

        public static ObjectValue ParseJson(string jsonStr)
        {
            if (jsonStr != null)
            {
                jsonStr = jsonStr.Replace('\n', ' ');
                jsonStr = jsonStr.Replace('\r', ' ');
                jsonStr = jsonStr.Replace('\t', ' ');
            }

            if (string.IsNullOrEmpty(jsonStr))
            {
                return null;
            }

            List<Tuple<string, ContainerValue>> containerStack = new List<Tuple<string, ContainerValue>>();
            ContainerValue activeContainer = null;
            Value activeValue = null;
            string activeKey = "";
            string strCurrent = "";

            bool bHasOpenedString = false;
            bool bHasStringValue = false;
            for (int Idx = 0; Idx < jsonStr.Length; Idx++)
            {
                if (jsonStr[Idx] == '"')
                {
                    bHasOpenedString = !bHasOpenedString;
                    bHasStringValue = true;
                    continue;
                }

                if (bHasOpenedString)
                {
                    strCurrent += jsonStr[Idx];
                    continue;
                }

                if (jsonStr[Idx] == ',' || jsonStr[Idx] == ']' || jsonStr[Idx] == '}')
                {
                    if (activeValue == null)
                    {
                        strCurrent = strCurrent.Trim();
                        if (strCurrent.Length > 0)
                        {
                            if (bHasStringValue)
                            {
                                activeValue = new StringValue(strCurrent);
                            }
                            else if (strCurrent.Equals("null", StringComparison.InvariantCultureIgnoreCase))
                            {
                                activeValue = new NullValue();
                            }
                            else if (strCurrent.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                            {
                                activeValue = new BoolValue(true);
                            }
                            else if (strCurrent.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                            {
                                activeValue = new BoolValue(false);
                            }
                            else if (strCurrent.Contains("."))
                            {
                                activeValue = new FloatValue(float.Parse(strCurrent));
                            }
                            else
                            {
                                activeValue = new IntValue(int.Parse(strCurrent));
                            }
                        }
                    }

                    if (activeContainer != null && activeValue != null)
                    {
                        bool needsKey = activeContainer is JsonParser.ObjectValue;
                        if (needsKey && string.IsNullOrEmpty(activeKey))
                        {
                            int snipStart = Math.Max(0, Idx - 20);
                            int snipEnd = Math.Min(Idx + 20, jsonStr.Length - 1);

                            Logger.WriteLine("Json parsing failed: Key is missing! (pos:{0}, container:{1}, value:{2}) snip[{3}...{4}]:'{5}'",
                                Idx,
                                containerStack.Count > 0 ? containerStack[containerStack.Count - 1].Item1 : "??",
                                activeValue.ToString(),
                                snipStart, snipEnd, jsonStr.Substring(snipStart, snipEnd - snipStart));
                        }
                        else
                        {
                            activeContainer.Add(activeKey, activeValue);
                        }
                    }

                    activeValue = null;
                    activeKey = null;
                    strCurrent = "";
                    bHasStringValue = false;
                }

                switch (jsonStr[Idx])
                {
                    case '{':
                        activeContainer = new ObjectValue();
                        containerStack.Add(new Tuple<string, ContainerValue>(activeKey, activeContainer));
                        activeKey = null;
                        break;

                    case '[':
                        activeContainer = new ArrayValue();
                        containerStack.Add(new Tuple<string, ContainerValue>(activeKey, activeContainer));
                        activeKey = null;
                        break;

                    case '}':
                    case ']':
                        activeValue = activeContainer;
                        activeKey = containerStack[containerStack.Count - 1].Item1;
                        containerStack.RemoveAt(containerStack.Count - 1);
                        activeContainer = (containerStack.Count == 0) ? null : containerStack[containerStack.Count - 1].Item2;
                        break;

                    case ':':
                        activeKey = strCurrent.Trim();
                        strCurrent = "";
                        bHasStringValue = false;
                        break;

                    case ',':
                        break;

                    default:
                        strCurrent += jsonStr[Idx];
                        break;
                }
            }

            return activeValue as ObjectValue;
        }
    }

    public class JsonWriter
    {
        private StringBuilder builder = new StringBuilder();
        private Stack<char> containerStack = new Stack<char>();
        private bool bNeedsSeparator = false;

        public override string ToString()
        {
            return builder.ToString();
        }

        public void WriteRawValue(string value, string key = null)
        {
            WriteKey(key);
            if (bNeedsSeparator) { builder.Append(','); bNeedsSeparator = false; }
            builder.Append(value);
            bNeedsSeparator = true;
        }

        public void WriteString(string value, string key = null)
        {
            WriteRawValue("\"" + value + "\"", key);
        }

        public void WriteBool(bool value, string key = null)
        {
            WriteRawValue(value ? "true" : "false", key);
        }

        public void WriteInt(int value, string key = null)
        {
            WriteRawValue(value.ToString(), key);
        }

        public void WriteFloat(float value, string key = null)
        {
            WriteRawValue(value.ToString(), key);
        }

        public void WriteNull(string key = null)
        {
            WriteRawValue("null", key);
        }

        public void WriteObjectStart(string key = null)
        {
            WriteKey(key);
            if (bNeedsSeparator) { builder.Append(','); bNeedsSeparator = false; }

            builder.Append('{');
            containerStack.Push('}');
        }

        public void WriteObjectEnd()
        {
            builder.Append(containerStack.Pop());
            bNeedsSeparator = true;
        }

        public void WriteArrayStart(string key = null)
        {
            WriteKey(key);
            if (bNeedsSeparator) { builder.Append(','); bNeedsSeparator = false; }

            builder.Append('[');
            containerStack.Push(']');
        }

        public void WriteArrayEnd()
        {
            builder.Append(containerStack.Pop());
            bNeedsSeparator = true;
        }

        private void WriteKey(string key = null)
        {
            if (bNeedsSeparator) { builder.Append(','); bNeedsSeparator = false; }
            if (!string.IsNullOrEmpty(key))
            {
                builder.Append('\"');
                builder.Append(key);
                builder.Append("\":");
            }
        }
    }
}
