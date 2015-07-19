using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SquidTracker.Web
{
    /// <summary>
    /// Control which only renders once per unique key on a given page.
    /// </summary>
    public class OnceTemplate : System.Web.UI.WebControls.PlaceHolder
    {
        public OnceTemplate() : base()
        {
        }

        public String Key { get; set; }

        private HashSet<String> m_keys = null;
        private HashSet<String> Keys
        {
            get
            {
                if (m_keys != null) return m_keys;
                if (!Page.Items.Contains("squidOnceTemplate"))
                {
                    m_keys = new HashSet<String>();
                    Page.Items.Add("squidOnceTemplate", m_keys);
                }
                else m_keys = (HashSet<String>)Page.Items["squidOnceTemplate"];
                return m_keys;
            }
        }

        protected override void Render(System.Web.UI.HtmlTextWriter writer)
        {
            if (Keys.Contains(Key)) return;
            Keys.Add(Key);
            base.Render(writer);
        }
    }
}
