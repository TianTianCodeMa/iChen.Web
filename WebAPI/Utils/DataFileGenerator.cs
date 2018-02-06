using System.Collections.Generic;
using System.IO;
using System.Text;
using iChen.Analytics;
using NPOI.SS.UserModel;

namespace iChen.Web.Analytics
{
	internal static class DataFileGenerator
	{
		public static string BuildCSVFile<T> (string[] headers, IEnumerable<T> data, double timezone, string delimiter = ",", bool escape = true) where T : IDataFileFormatConverter
		{
			var sb = new StringBuilder();
			sb.AppendLine(string.Join(delimiter, headers));

			foreach (var line in data) sb.AppendLine(line.ToCSVDataLine(headers, timezone, delimiter, escape));

			return sb.ToString();
		}

		public static void BuildXLSFile<T> (Stream stream, IWorkbook xls, string sheet, string[] headers, IEnumerable<T> data, double timezone) where T : IDataFileFormatConverter
		{
			var ss = xls.CreateSheet(sheet);

			var header = ss.CreateRow(0);
			var col = 0;

			foreach (var field in headers) {
				var cell = header.CreateCell(col++);
				cell.SetCellValue(field);
			}

			var dateformat = xls.CreateDataFormat().GetFormat("yyyy-MM-dd HH:mm:ss");
			var datestyle = xls.CreateCellStyle();
			datestyle.DataFormat = dateformat;

			var row = 1;

			foreach (var record in data) {
				var line = ss.CreateRow(row);
				record.FillXlsRow(xls, ss, headers, line, datestyle, timezone);
				row++;
			}

			xls.Write(stream);
		}
	}
}