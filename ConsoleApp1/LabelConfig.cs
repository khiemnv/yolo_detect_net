using ClosedXML.Excel;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ConsoleApp1
{
    public class LabelConfig
    {
        public IDictionary<int, string> _dict = new Dictionary<int, string>();
        public IDictionary<string, string> _dict2 = new Dictionary<string, string>();
        public Dictionary<string, int> d;
        public Dictionary<int, string> ReadDictModelExcelObj(string path, string shName = "DictModel")
        {
            Dictionary<int, string> objNums = new Dictionary<int, string>();
            var wb = new XLWorkbook(Path.Combine(path));
            if (!wb.Worksheets.Contains(shName))
            {
                return null;
            }
            var ws = wb.Worksheets.Worksheet(shName);
            var len = ws.LastRowUsed().RowNumber();
            for (int i = 1; i < len; i++)
            {
                int value = ws.Cell(i + 1, 1).GetValue<int>();
                string name = ws.Cell(i + 1, 2).GetValue<string>();
                objNums.Add(value, name);
            }

            // read alias
            _dict2.Clear();
            for (int i = 1; i < len; i++)
            {
                string alias = ws.Cell(i + 1, 4).GetValue<string>();
                if (string.IsNullOrEmpty(alias)) { break; }

                string name = ws.Cell(i + 1, 2).GetValue<string>();
                _dict2.Add(name, alias);
            }

            _dict = objNums;
            return objNums;
        }


    }
}
