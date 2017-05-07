using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

/// <summary>
/// Advanced Binary Tools - By Marcussacana
/// </summary>
namespace AdvancedBinary {

    enum StringStyle {
        /// <summary>
        /// C-Style String (null terminated)
        /// </summary>
        CString,
        /// <summary>
        /// Unicode C-Style String (null terminated 2x)
        /// </summary>
        UCString,
        /// <summary>
        /// Pascal-Style String (int32 Length Prefix)
        /// </summary>
        PString
    }


    /// <summary>
    /// InvokeMethod While Reading
    /// </summary>
    /// <param name="Stream">Stream Instance</param>
    /// <param name="FromReader">Determine if the method is invoked from the StructReader or StructWriter</param>
    /// <param name="StructInstance">Struct instance reference</param>
    /// <return>Struct Instance</return>
    public delegate dynamic FieldInvoke(Stream Stream, bool FromReader, dynamic StructInstance);
    
    /// <summary>
    /// Ignore Struct Field
    /// </summary>
    public class Ignore : Attribute { }

    /// <summary>
    /// C-Style String (null terminated)
    /// </summary>
    public class CString : Attribute { }

    /// <summary>
    /// Unicode C-Style String (null terminated 2x)
    /// </summary>
    public class UCString : Attribute { }
    /// <summary>
    /// Pascal-Style String (int32 Length Prefix)
    /// </summary>
    public class PString : Attribute { }

    /// <summary>
    /// Struct Field Type (required only to sub structs)
    /// </summary>
    public class StructField : Attribute { }
    internal static class Const {
        //Types
        public const string INT8 = "System.Byte";
        public const string UINT8 = "System.SByte";
        public const string INT32 = "System.Int32";
        public const string UINT32 = "System.UInt32";
        public const string DOUBLE = "System.Double";
        public const string FLOAT = "System.Single";
        public const string INT64 = "System.Int64";
        public const string UINT64 = "System.UInt64";
        public const string STRING = "System.String";
        public const string DELEGATE = "System.MulticastDelegate";

        //Attributes
        public const string PSTRING = "PString";
        public const string CSTRING = "CString";
        public const string UCSTRING = "UCString";
        public const string STRUCT = "StructField";
        public const string IGNORE = "Ignore";
    }
    
    static class Tools {
        /* Change Attributes at runtime is a really shit and i need write a unsafe code... I give-up
        public static dynamic GetAttributePropertyValue(ref dynamic Struct, string FieldName, string AttributeName, string PropertyName) {
            Type t = Struct.GetType();
            FieldInfo[] Fields = t.GetFields();
            foreach (FieldInfo Fld in Fields) {
                if (Fld.Name != FieldName)
                    continue;
                foreach (Attribute tmp in Fld.GetCustomAttributes(true)) {
                    Type Attrib = tmp.GetType();
                    if (Attrib.Name != AttributeName)
                        continue;
                    foreach (FieldInfo Field in Attrib.GetFields()) {
                        if (Field.Name != PropertyName)
                            continue;
                        return Field.GetValue(tmp);
                    }
                    throw new Exception("Property Not Found");
                }
                throw new Exception("Attribute Not Found");
            }
            throw new Exception("Field Not Found");
        }*/
        
        public static long GetStructLength<T>(T Struct, int StringLen = -1) {
            Type type = Struct.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            long Length = 0;
            foreach (FieldInfo field in fields) {
                if (HasAttribute(field, Const.IGNORE))
                    continue;
                switch (field.FieldType.ToString()) {
                    case Const.INT8:
                    case Const.UINT8:
                        Length += 1;
                        break;
                    case Const.INT32:
                    case Const.FLOAT:
                    case Const.UINT32:
                        Length += 4;
                        break;
                    case Const.UINT64:
                    case Const.INT64:
                    case Const.DOUBLE:
                        Length += 8;
                        break;
                    case Const.STRING:
                        if (StringLen == -1)
                            throw new Exception("You can't calculate struct length with strings");
                        else
                            Length += StringLen;
                        break;
                    default:
                        if (field.FieldType.BaseType.ToString() == Const.DELEGATE)
                            break;
                        if (HasAttribute(field, Const.IGNORE))
                            break;
                        throw new Exception("Unk Struct Field: " + field.FieldType.ToString());
                }
            }
            return Length;
        }

        internal static bool HasAttribute(FieldInfo Field, string Attrib) {
            foreach (Attribute attrib in Field.GetCustomAttributes(true))
                if (attrib.GetType().Name == Attrib)
                    return true;
            return false;
        }
        public static void ReadStruct(byte[] Array, ref object Struct, Encoding Encoding = null) {
            MemoryStream Stream = new MemoryStream(Array);
            StructReader Reader = new StructReader(Stream, Encoding);
            Reader.ReadStruct(ref Struct);
            Reader.Close();
            Stream?.Close();
        }

        public static byte[] BuildStruct(ref object Struct, Encoding Encoding = null) {
            MemoryStream Stream = new MemoryStream();
            StructWriter Writer = new StructWriter(Stream, Encoding);
            Writer.WriteStruct(ref Struct);
            byte[] Result = Stream.ToArray();
            Writer.Close();
            Stream?.Close();
            return Result;
        }
    }

    class StructWriter : BinaryWriter {

        private Encoding Encoding;
        public StructWriter(Stream Input, Encoding Encoding = null) : base(Input) {
            if (Encoding == null)
                Encoding = Encoding.UTF8;
            this.Encoding = Encoding;
        }

        public void WriteStruct<T>(ref T Struct) {
            Type type = Struct.GetType();
            object tmp = Struct;
            WriteStruct(type, ref tmp);
            Struct = (T)tmp;
        }

        private void WriteStruct(Type type, ref object Instance) {
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (HasAttribute(field, Const.IGNORE))
                    break;
                dynamic Value = field.GetValue(Instance);
                switch (field.FieldType.ToString()) {
                    case Const.STRING:
                        if (HasAttribute(field, Const.CSTRING) && HasAttribute(field, Const.PSTRING))
                            throw new Exception("You can't use CString and PString Attribute into the same field.");
                        if (HasAttribute(field, Const.CSTRING)) {
                            Write(Value, StringStyle.CString);
                            break;
                        }
                        if (HasAttribute(field, Const.UCSTRING)) {
                            Write(Value, StringStyle.UCString);
                            break;
                        }
                        if (HasAttribute(field, Const.PSTRING)) {
                            Write(Value, StringStyle.PString);
                            break;
                        }
                        throw new Exception("String Attribute Not Specified.");
                    default:
                        if (HasAttribute(field, Const.STRUCT)) {
                            WriteStruct(field.FieldType, ref Value);
                        } else {
                            if (field.FieldType.BaseType.ToString() == Const.DELEGATE) {
                                FieldInvoke Invoker = ((FieldInvoke)Value);
                                if (Invoker == null)
                                    break;
                                Instance = Invoker.Invoke(BaseStream, false, Instance);
                                field.SetValue(Instance, Invoker);
                            } else
                                Write(Value);
                        }
                        break;
                }
            }
        }


        public void Write(string String, StringStyle Style) {
            switch (Style) {
                case StringStyle.UCString:
                case StringStyle.CString:
                    List<byte> Buffer = new List<byte>(this.Encoding.GetBytes(String + "\x0"));
                    base.Write(Buffer.ToArray(), 0, Buffer.Count);
                    break;
                default:
                    base.Write(String);
                    break;
            }
        }
        private bool HasAttribute(FieldInfo Field, string Attrib) {
            foreach (Attribute attrib in Field.GetCustomAttributes(true))
                if (attrib.GetType().Name == Attrib)
                    return true;
            return false;
        }

        internal void Seek(long Index, SeekOrigin Origin) {
            base.BaseStream.Seek(Index, Origin);
            base.BaseStream.Flush();
        }
    }

    class StructReader : BinaryReader {

        private Encoding Encoding;
        public StructReader(Stream Input, Encoding Encoding = null) : base(Input) {
            if (Encoding == null)
                Encoding = Encoding.UTF8;
            this.Encoding = Encoding;
        }

        public void ReadStruct<T>(ref T Struct) {
            Type type = Struct.GetType();
            object tmp = Struct;
            ReadStruct(type, ref tmp);
            Struct = (T)tmp;
        }

        private void ReadStruct(Type type, ref object Instance) {
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (Tools.HasAttribute(field, Const.IGNORE))
                    break;
                object Value = null;
                switch (field.FieldType.ToString()) {
                    case Const.INT8:
                        Value = base.ReadSByte();
                        break;
                    case Const.UINT8:
                        Value = base.ReadByte();
                        break;
                    case Const.INT32:
                        Value = base.ReadInt32();
                        break;
                    case Const.UINT32:
                        Value = base.ReadUInt32();
                        break;
                    case Const.DOUBLE:
                        Value = base.ReadDouble();
                        break;
                    case Const.FLOAT:
                        Value = base.ReadSingle();
                        break;
                    case Const.INT64:
                        Value = base.ReadInt64();
                        break;
                    case Const.UINT64:
                        Value = base.ReadUInt64();
                        break;
                    case Const.STRING:
                        if (Tools.HasAttribute(field, Const.CSTRING) && Tools.HasAttribute(field, Const.PSTRING))
                            throw new Exception("You can't use CString and PString Attribute into the same field.");
                        if (Tools.HasAttribute(field, Const.CSTRING)) {
                            Value = ReadString(StringStyle.CString);
                            break;
                        }
                        if (Tools.HasAttribute(field, Const.UCSTRING)) {
                            Value = ReadString(StringStyle.UCString);
                            break;
                        }
                        if (Tools.HasAttribute(field, Const.PSTRING)) {
                            Value = ReadString(StringStyle.PString);
                            break;
                        }
                        throw new Exception("String Attribute Not Specified.");
                    default:
                        if (Tools.HasAttribute(field, Const.STRUCT)) {
                            Value = Activator.CreateInstance(field.FieldType);
                            ReadStruct(field.FieldType, ref Value);
                        } else {
                            if (field.FieldType.BaseType.ToString() == Const.DELEGATE) {
                                FieldInvoke Invoker = (FieldInvoke)field.GetValue(Instance);
                                Value = Invoker;
                                if (Invoker == null)
                                    break;
                                Instance = Invoker.Invoke(BaseStream, true, Instance);                                
                                break;
                            }
                            throw new Exception("Unk Struct Field: " + field.FieldType.ToString());
                        }
                        break;
                }
                field.SetValue(Instance, Value);
            }
        }


        public string ReadString(StringStyle Style) {
            List<byte> Buffer = new List<byte>();
            switch (Style) {
                case StringStyle.CString:
                    while (true) {
                        byte Byte = base.ReadByte();
                        if (Byte < 1)
                            break;
                        Buffer.Add(Byte);
                    }
                    return Encoding.GetString(Buffer.ToArray());
                case StringStyle.UCString:
                    while (true) {
                        byte Byte1 = base.ReadByte();
                        byte Byte2 = base.ReadByte();
                        if (Byte1 == 0x00 && Byte2 == 0x00)
                            break;
                        Buffer.Add(Byte1);
                        Buffer.Add(Byte2);
                    }
                    return Encoding.GetString(Buffer.ToArray());
                default:
                    return base.ReadString();
            }
        }

        internal void Seek(long Index, SeekOrigin Origin) {
            base.BaseStream.Seek(Index, Origin);
            base.BaseStream.Flush();
        }
    }
}
