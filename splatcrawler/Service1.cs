using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SquidTracker.Crawler;
using SquidTracker.Data;

namespace splatcrawler
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        private Thread m_worker = null;
        private bool m_should_work;

        protected override void OnStart(string[] args)
        {
            if (m_worker != null) return;
            m_worker = new Thread(Work);
            m_should_work = true;
            m_worker.Start();
        }

        protected override void OnStop()
        {
            if (m_worker == null) return;
            m_should_work = false;
            if (m_worker != null) Thread.Sleep(100);
            while (m_worker != null) Thread.Sleep(1000);
        }

        private void Work()
        {
            DateTime nextPollTime = DateTime.MinValue;
            PollTask[] tasks = new PollTask[]
            {
                new StagesInfoTask(),
                new FesInfoTask(),
            };

            while (m_should_work)
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    foreach (PollTask task in tasks.Where(t => t.NextPollTime <= now))
                    {
                        try
                        {
                            task.Run();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                Thread.Sleep(1000);
            }
            m_worker = null;
        }
    }
}
