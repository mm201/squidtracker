using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using SquidTracker.Data;

namespace SquidTracker.Web
{
    public partial class Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();
                litConversionScript.Text = RenderConversionTables(conn);
                conn.Close();
            }
        }

        private static String RenderConversionTables(MySqlConnection conn)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<script type=\"text/javascript\">");

            WriteConversionArray(builder, conn.ExecuteDataTable("SELECT identifier, filename, name_ja, name_en FROM squid_stages ORDER BY filename, identifier"), "squidStages");
            /*
            WriteConversionArray(builder, conn.ExecuteDataTable("SELECT identifier, filename, name_ja, name_en FROM squid_gear_head ORDER BY filename, identifier"), "squidGearHead");
            WriteConversionArray(builder, conn.ExecuteDataTable("SELECT identifier, filename, name_ja, name_en FROM squid_gear_clothes ORDER BY filename, identifier"), "squidGearClothes");
            WriteConversionArray(builder, conn.ExecuteDataTable("SELECT identifier, filename, name_ja, name_en FROM squid_gear_shoes ORDER BY filename, identifier"), "squidGearShoes");
            WriteConversionArray(builder, conn.ExecuteDataTable("SELECT identifier, filename, name_ja, name_en FROM squid_weapons ORDER BY filename, identifier"), "squidWeapons");
            */

            builder.Append("</script>");

            return builder.ToString();
        }

        private static void WriteConversionArray(StringBuilder builder, DataTable tbl, String objectName)
        {
            builder.Append("var ");
            builder.Append(objectName);
            builder.Append("={\n");

            bool notFirst = false;

            foreach (DataRow row in tbl.Rows)
            {
                if (notFirst) builder.Append(",\n");
                WriteStringValue(builder, DatabaseExtender.Cast<String>(row["identifier"]));
                builder.Append(":{\"filename\":");
                WriteStringValue(builder, DatabaseExtender.Cast<String>(row["filename"]));
                builder.Append(",\"name_ja\":");
                WriteStringValue(builder, DatabaseExtender.Cast<String>(row["name_ja"]));
                builder.Append(",\"name_en\":");
                WriteStringValue(builder, DatabaseExtender.Cast<String>(row["name_en"]));
                builder.Append("}");
                notFirst = true;
            }

            builder.Append("};\n");
        }

        private static void WriteStringValue(StringBuilder builder, String value)
        {
            if (value == null) builder.Append("null");
            else
            {
                builder.Append("\"");
                builder.Append(Common.JsEncode(value));
                builder.Append("\"");
            }
        }
    }
}
