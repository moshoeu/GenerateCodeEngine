/******************************************************************
** 文件名:  GenCodeTplBase
** 版  权:  (C)  
** 创建人:  moshoeu
** 日  期:  2021/7/22 23:43:44
** 描  述: 	

**************************** 修改记录 ******************************
** 修改人: 
** 日  期: 
** 描  述: 
*******************************************************************/

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Framework.Editor
{
    public abstract class GenCodeTplBase
    {
        public StringBuilder m_FileBuilder = new StringBuilder();

        /// <summary>
        /// 模板使用的数据
        /// </summary>
        public object m_Data;

        /// <summary>
        /// 生成代码入口
        /// </summary>
        public abstract void Build(out string fileName);

        /// <summary>
        /// 获取完整的类名
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        protected string GetFullTypeName(Type type, string[] ignoreNamespace = null)
        {
            var objType = typeof(object);
            var valueType = typeof(ValueType);

            if (type.IsArray)
            {
                string arraySign = "";
                for (int i = 1; i < type.GetArrayRank(); i++)
                {
                    arraySign += ",";
                }

                var elmType = type.GetElementType();
                string elmTypeName = GetFullTypeName(elmType);
                if (elmType.IsArray)
                {
                    int bracketIdx = elmTypeName.IndexOf('[');

                    return $"{elmTypeName.Substring(0, bracketIdx)}[{arraySign}]" +
                        $"{elmTypeName.Substring(bracketIdx)}";
                }
                else
                {
                    return $"{elmTypeName}[{arraySign}]";
                }

            }
            else if (type.IsByRef)
            {
                return GetFullTypeName(type.GetElementType());
            }
            else if (type.IsGenericParameter)
            {
                if (type.BaseType != objType && type.BaseType != valueType)
                {
                    return GetFullTypeName(type.BaseType);
                }
            }

            string name = GetNameWithNamespace(type);
            if (type.IsGenericType)
            {
                string genericParameter = "";
                
                var genericParameterTypes = type.GetGenericArguments();
                for (int i = 0; i < genericParameterTypes.Length; i++)
                {
                    string prefix = i == 0 ? "" : ", ";
                    string genericParameterTypeName = GetFullTypeName(genericParameterTypes[i]);
                    genericParameter = $"{genericParameter}{prefix}{genericParameterTypeName}";
                }

                name = new Regex(@"`\d+").Replace(name, $"<{genericParameter}>");
                name = new Regex(@"\[.*?\]").Replace(name, "");

                return name;
            }
            else
            {
                return m_friendlyNameMap.TryGetValue(name, out string friendlyName) ?
                    friendlyName : name;
            }

            string GetNameWithNamespace(Type type)
            {
                if (ignoreNamespace != null)
                {
                    if (ignoreNamespace.Any(n => n == type.Namespace))
                    {
                        return type.Name;
                    }
                }

                return $"{type.Namespace}.{type.Name}"; ;
            }
        }

        private static readonly Dictionary<string, string> m_friendlyNameMap =
            new Dictionary<string, string>()
            {
                ["System.Object"] = "object",
                ["System.String"] = "string",
                ["System.Boolean"] = "bool",
                ["System.Byte"] = "byte",
                ["System.Char"] = "char",
                ["System.Decimal"] = "decimal",
                ["System.Double"] = "double",
                ["System.Int16"] = "short",
                ["System.Int32"] = "int",
                ["System.Int64"] = "long",
                ["System.SByte"] = "sbyte",
                ["System.Single"] = "float",
                ["System.UInt16"] = "ushort",
                ["System.UInt32"] = "uint",
                ["System.UInt64"] = "ulong",
                ["System.Void"] = "void",
            };

    }
}
