/******************************************************************
** 文件名:  GenCodeTools
** 版  权:  (C)  
** 创建人:  moshoeu
** 日  期:  2021/7/22 
** 描  述: 	代码生成工具 参考xlua的生成引擎 https://github.com/Tencent/xLua

**************************** 修改记录 ******************************
** 修改人: 
** 日  期: 
** 描  述: 
*******************************************************************/

using System;
using System.Reflection;
using UnityEngine;
using System.IO;
using UnityEditor.Compilation;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace Framework.Editor
{
    public class GenCodeTools
    {
        /// <summary>
        /// 临时dll文件夹路径
        /// </summary>
        static readonly string PATH_TMP_ASSEMBLY = "Temp/Assembly";

        /// <summary>
        /// 临时dll路径
        /// </summary>
        static readonly string PATH_TMP_ASSEMBLY_FILE = $"{PATH_TMP_ASSEMBLY}/TmpDLL.dll";

        /// <summary>
        /// unity editor程序集
        /// </summary>
        const string PATH_UNITY_EDITOR_ASSEMBLY_FILE = "Library/ScriptAssemblies/Assembly-CSharp-Editor.dll";

        /// <summary>
        /// 生成代码
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="filePath"></param>
        public static void GenCode(GenCodeTask[] tasks, bool isSync = true)
        {
            // 为每个任务解析模板 生成临时文件
            foreach (GenCodeTask task in tasks)
            {
                List<Chunk> chunks = ParseTplTxt(task.m_TplFilePath);
                BuildTmpFile(chunks, task.TmpFilePath);
            }

            // 编译临时文件 输出生成代码
            Compile(tasks, isSync);
        }

        //[UnityEditor.MenuItem("自定义工具/测试生成代码")]
        //public static void TestGenCode()
        //{
        //    GenCodeTask[] tasks = new GenCodeTask[1]
        //    {
        //        new GenCodeTask("GenCodeTestTpl", "Assets/Scripts/GenerateCodeEngine/Editor/GenCodeTpl/GenCodeTestTpl.txt",
        //        "Assets/Scripts", "TestOutput")
        //    };

        //    GenCode(tasks, true);
        //}

        #region 构建代码

        /// <summary>
        /// 编译代码 构建临时程序集
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isSync"></param>
        static void Compile(GenCodeTask[] tasks, bool isSync = true)
        {
            string[] filePaths =  tasks.Select((task) => task.TmpFilePath).ToArray();

            Directory.CreateDirectory(PATH_TMP_ASSEMBLY);

            AssemblyBuilder assemblyBuilder = new AssemblyBuilder(PATH_TMP_ASSEMBLY_FILE, filePaths);

            assemblyBuilder.additionalReferences = new string[1] { PATH_UNITY_EDITOR_ASSEMBLY_FILE };

            assemblyBuilder.buildStarted += (assemblyPath) =>
            {
                Debug.Log($"GenCodeTools.cs : 开始构建临时程序集 {assemblyPath}");
            };

            assemblyBuilder.buildFinished += (assemblyPath, msgs) =>
            {
                Debug.Log($"GenCodeTools.cs : 结束构建临时程序集 {assemblyPath}");
                foreach (var msg in msgs)
                {
                    if (msg.type == CompilerMessageType.Error)
                    {
                        Debug.LogError(msg.message);
                    }
                    else if (msg.type == CompilerMessageType.Warning)
                    {
                        Debug.LogWarning(msg.message);
                    }
                }

                // 加载临时程序集 
                System.Reflection.Assembly dll = System.Reflection.Assembly.LoadFile(assemblyPath);
                foreach (GenCodeTask task in tasks)
                {
                    // 执行模板代码 输出生成代码
                    BuildOutputFile(task, dll);

                    // 销毁任务
                    task.Destroy();
                }

                UnityEditor.AssetDatabase.Refresh();

                //// 销毁临时程序集
                //File.Delete(assemblyPath);
            };

            if (false == assemblyBuilder.Build())
            {
                Debug.LogError($"GenCodeTools.cs : 未能成功开始构建程序集，请确保unity未在编译状态");
                return;
            }

            // 同步构建 线程挂起等待构建完成
            if (true == isSync)
            {
                while (assemblyBuilder.status != AssemblyBuilderStatus.Finished)
                {
                    System.Threading.Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// 检查模板类型名
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        static bool CheckTplTypeName(string typeName, System.Reflection.Assembly dll)
        { 
            Type tplType = dll.GetType(typeName);
            if (tplType == null)
            {
                Debug.LogError($"GenCodeTools.cs : 类型名为[{typeName}]的类型不存在！");
                return false;
            }

            if (tplType.BaseType != typeof(GenCodeTplBase))
            {
                Debug.LogError($"GenCodeTools.cs : 类型名为[{typeName}]的类型没有直接继承[GenCodeTplBase]类！");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// 组建输出文件
        /// </summary>
        /// <param name="task"></param>
        /// <param name="dll"></param>
        static void BuildOutputFile(GenCodeTask task, System.Reflection.Assembly dll)
        {
            if (false == CheckTplTypeName(task.m_TplTypeName, dll))
            {
                return;
            }

            GenCodeTplBase taskTypeInst = dll.CreateInstance(task.m_TplTypeName) as GenCodeTplBase;
            taskTypeInst.m_Data = task.m_Data;

            //Type taskType = taskTypeInst.GetType();

            //MethodInfo method = taskType.GetMethod(NAME_TPL_BUILD_METHOD);
            //method.Invoke(taskTypeInst, null);

            string fileName;
            taskTypeInst.Build(out fileName);

            string outputDir = task.OutputDirPath;
            if (!outputDir.EndsWith("/") && !outputDir.EndsWith(@"\\"))
            {
                outputDir = $"{outputDir}/";
            }

            File.WriteAllText($"{outputDir}{fileName}{task.m_OutputExtension}", taskTypeInst.m_FileBuilder.ToString());
        }

        #endregion

        #region 解析模板

        private enum TokenType
        {
            Code, Eval, Text
        }

        private class Chunk
        {
            public TokenType Type { get; private set; }
            public string Text { get; private set; }
            public Chunk(TokenType type, string text)
            {
                Type = type;
                Text = text;
            }
        }


        static string m_regexString;

        /// <summary>
        /// 正则匹配
        /// </summary>
        /// <returns></returns>
        static string RegexString
        {
            get
            {
                if (string.IsNullOrEmpty(m_regexString))
                {
                    string regexBadUnopened = @"(?<error>((?!<%).)*%>)";
                    string regexText = @"(?<code>((?!<%).)+)";
                    string regexNoCode = @"(?<nocode><%=?%>)";
                    string regexCode = @"<%(?<text>[^=]((?!<%|%>).)*)%>";
                    string regexEval = @"<%=(?<eval>((?!<%|%>).)*)%>";
                    string regexBadUnclosed = @"(?<error><%.*)";
                    string regexBadEmpty = @"(?<error>^$)";

                    m_regexString = $"({regexBadUnopened}|{regexText}|{regexNoCode}|{regexCode}|{regexEval}|{regexBadUnclosed}|{regexBadEmpty})*";
                }

                return m_regexString;
            }
        }

        /// <summary>
        /// 解析模板文本
        /// </summary>
        /// <param name="tplFilePath"></param>
        /// <returns></returns>
        static List<Chunk> ParseTplTxt(string tplFilePath)
        {
            string tplFileTxt = File.ReadAllText(tplFilePath);

            Regex templateRegex = new Regex(
                RegexString,
                RegexOptions.ExplicitCapture | RegexOptions.Singleline
            );
            Match matches = templateRegex.Match(tplFileTxt);

            if (matches.Groups["error"].Length > 0)
            {
                var error = matches.Groups["error"];
                throw new Exception($"模板[{tplFilePath}]配置错误，请检查语法格式！");
            }

            List<Chunk> Chunks = matches.Groups["code"].Captures
                .Cast<Capture>()
                .Select(p => new { Type = TokenType.Code, p.Value, p.Index })
                .Concat(matches.Groups["text"].Captures
                .Cast<Capture>()
                .Select(p => new { Type = TokenType.Text, Value = EscapeString(p.Value), p.Index }))
                .Concat(matches.Groups["eval"].Captures
                .Cast<Capture>()
                .Select(p => new { Type = TokenType.Eval, p.Value, p.Index }))
                .OrderBy(p => p.Index)
                .Select(m => new Chunk(m.Type, m.Value))
                .ToList();

            if (Chunks.Count == 0)
            {
                throw new Exception($"模板[{tplFilePath}]为空");
            }
            return Chunks;
        }

        /// <summary>
        /// 构建临时文件
        /// </summary>
        /// <param name="chunks"></param>
        static void BuildTmpFile(List<Chunk> chunks, string tmpFilePath)
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach (Chunk chunk in chunks)
            {
                switch (chunk.Type)
                {
                    case TokenType.Code:
                        {
                            stringBuilder.AppendLine(chunk.Text);
                        }
                        break;
                    case TokenType.Eval:
                        {
                            stringBuilder.AppendLine($"m_FileBuilder.Append({chunk.Text}.ToString());");
                        }
                        break;
                    case TokenType.Text:
                        {
                            stringBuilder.AppendLine($"m_FileBuilder.Append(\"{chunk.Text}\");");
                        }
                        break;
                }
            }

            File.WriteAllText(tmpFilePath, stringBuilder.ToString());
        }

        /// <summary>
        /// 替换转义字符
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        static string EscapeString(string input)
        {
            var output = input
                // 自定义的转义符 为了实现生成的代码文件换行
                .Replace("@n", "\n")
                .Replace("@t", "\t")

                // 替换原有转义符
                .Replace("\\", @"\\")
                .Replace("\'", @"\'")
                .Replace("\"", @"\""")
                .Replace("\n", @"\n")
                .Replace("\t", @"\t")
                .Replace("\r", @"\r")
                .Replace("\b", @"\b")
                .Replace("\f", @"\f")
                .Replace("\a", @"\a")
                .Replace("\v", @"\v")
                .Replace("\0", @"\0");
            return output;
        }

        #endregion

    }
}

