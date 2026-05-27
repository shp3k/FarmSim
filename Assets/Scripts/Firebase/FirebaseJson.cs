using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

public static class FirebaseJson
{
    public static string ToJson(object value)
    {
        StringBuilder builder = new();
        WriteValue(builder, value);
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, object value)
    {
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        switch (value)
        {
            case string stringValue:
                WriteString(builder, stringValue);
                return;
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                return;
            case char charValue:
                WriteString(builder, charValue.ToString());
                return;
        }

        Type type = value.GetType();
        if (IsNumeric(type))
        {
            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            return;
        }

        if (value is IDictionary dictionary)
        {
            WriteDictionary(builder, dictionary);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            WriteEnumerable(builder, enumerable);
            return;
        }

        WriteObject(builder, value, type);
    }

    private static void WriteDictionary(StringBuilder builder, IDictionary dictionary)
    {
        builder.Append('{');
        bool first = true;
        foreach (DictionaryEntry entry in dictionary)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
            builder.Append(':');
            WriteValue(builder, entry.Value);
        }

        builder.Append('}');
    }

    private static void WriteEnumerable(StringBuilder builder, IEnumerable enumerable)
    {
        builder.Append('[');
        bool first = true;
        foreach (object item in enumerable)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteValue(builder, item);
        }

        builder.Append(']');
    }

    private static void WriteObject(StringBuilder builder, object value, Type type)
    {
        builder.Append('{');
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        bool first = true;
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field.IsNotSerialized)
            {
                continue;
            }

            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteString(builder, field.Name);
            builder.Append(':');
            WriteValue(builder, field.GetValue(value));
        }

        builder.Append('}');
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        if (!string.IsNullOrEmpty(value))
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
        }

        builder.Append('"');
    }

    private static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }
}
