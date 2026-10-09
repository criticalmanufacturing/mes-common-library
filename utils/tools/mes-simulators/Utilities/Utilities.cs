using Cmf.Foundation.BusinessObjects;
using System.Data;
using System.Xml;
using System.Xml.Linq;

namespace MESSimulator
{
    public static class Utilities
    {
        // The XML comes from the MES: no DTD, no external resources
        private static readonly XmlReaderSettings safeXml = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        /// <summary>
        /// Convert a NgpDataSet to a DataSet
        /// </summary>
        /// <param name="dsd">NgpDataSet to convert</param>
        /// <returns>Returns a DataSet with all information of the NgpDataSet</returns>
        public static DataSet ToDataSet(NgpDataSet dsd)
        {
            var ds = new DataSet();

            //Insert schema
            using (var schema = XmlReader.Create(new StringReader(dsd.XMLSchema), safeXml))
            {
                ds.ReadXmlSchema(schema);
            }
            if (string.IsNullOrEmpty(dsd.DataXML))
            {
                return ds;
            }

            //Insert data
            using (var data = XmlReader.Create(new StringReader(dsd.DataXML), safeXml))
            {
                ds.ReadXml(data);
            }
            XDocument xd;
            using (var data = XmlReader.Create(new StringReader(dsd.DataXML), safeXml))
            {
                xd = XDocument.Load(data);
            }

            foreach (DataTable dt in ds.Tables)
            {
                // A table's rows are the root's direct children named after it: a nested element (or a column) with the
                // same name is not a row
                var rows = xd.Root?.Elements(dt.TableName).ToList() ?? [];
                for (int i = 0; i < rows.Count && i < dt.Rows.Count; i++)
                {
                    var rowState = rows[i].Attribute("RowState")?.Value;
                    var state = rowState != null && Enum.TryParse<DataRowState>(rowState, out var parsed) ? parsed : DataRowState.Added;

                    DataRow dr = dt.Rows[i];
                    dr.AcceptChanges();

                    if (state == DataRowState.Deleted)
                    {
                        dr.Delete();
                    }
                    else if (state == DataRowState.Added)
                    {
                        dr.SetAdded();
                    }
                    else if (state == DataRowState.Modified)
                    {
                        dr.SetModified();
                    }
                }
            }

            return ds;
        }
    }
}
