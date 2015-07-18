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

        protected override void OnStart(string[] args)
        {
            Start();
        }

        public void Start()
        {
            // todo: thread me
            DateTime nextPollTime = DateTime.MinValue;
            PollTask[] tasks = new PollTask[]
            {
                new StagesInfoTask(),
                new FesInfoTask(),
            };

            while (true)
            {
                DateTime now = DateTime.UtcNow;
                foreach (PollTask task in tasks.Where(t => t.NextPollTime <= now))
                {
                    task.Run();
                }
                Thread.Sleep(1000);
            }
        }

        protected override void OnStop()
        {
        }
    }
}
