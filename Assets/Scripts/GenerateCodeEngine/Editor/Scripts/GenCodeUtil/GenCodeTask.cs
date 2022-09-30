/******************************************************************
** 文件名:  GenCodeTask
** 版  权:  (C)  
** 创建人:  moshoeu
** 日  期:  2021/7/25 1:59:31
** 描  述: 	代码生成任务

**************************** 修改记录 ******************************
** 修改人: 
** 日  期: 
** 描  述: 
*******************************************************************/

using System.IO;

namespace Framework.Editor
{
    public class GenCodeTask
    {
        /// <summary>
        /// 模板类型名字
        /// </summary>
        public readonly string m_TplTypeName;

        /// <summary>
        /// 模板文件路径
        /// </summary>
        public readonly string m_TplFilePath;

        /// <summary>
        /// 输出文件夹路径
        /// </summary>
        public readonly string m_OutputDirPath;

        /// <summary>
        /// 输出文件拓展名
        /// </summary>
        public readonly string m_OutputExtension;

        /// <summary>
        /// 模板类需要的数据
        /// </summary>
        public readonly object m_Data;

        /// <summary>
        /// 临时文件路径
        /// </summary>
        public string TmpFilePath
        {
            get
            {
                return m_TplFilePath.Replace(".txt", ".cs");
            }
        }

        /// <summary>
        /// 输出脚本路径
        /// </summary>
        public string OutputDirPath
        {
            get
            {
                return m_OutputDirPath;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tplTypeName">模板类名</param>
        /// <param name="tplFilePath">模板文件路径</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="outputExtension">输出文件拓展名</param>
        /// <param name="data">模板需要用到的数据</param>
        public GenCodeTask(string tplTypeName, string tplFilePath, string outputPath, string outputExtension, object data = null)
        {
            m_TplTypeName = tplTypeName;
            m_TplFilePath = tplFilePath;
            m_OutputDirPath = outputPath;
            m_OutputExtension = outputExtension;

            m_Data = data;
        }

        public void Destroy()
        {
            string tmpFilePath = TmpFilePath;

            // 删除临时文件
            if (false == string.IsNullOrEmpty(tmpFilePath) &&
                File.Exists(tmpFilePath))
            {
                File.Delete(tmpFilePath);
            }
        }
    }
}
