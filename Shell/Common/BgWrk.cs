using System.ComponentModel;

namespace annotation
{
    public class BgWrk
    {
        // backgroud
        private BackgroundWorker bg;
        public delegate object WorkCallback(object arg);
        public delegate void CompleteCallback(object result);
        public delegate void ReportCallback(object result);

        private class BgParam
        {
            public WorkCallback w;
            public CompleteCallback c;
            public ReportCallback p;
            public object r;
            public object s;
        }
        private BgParam sectionParam;
        public bool BgExecute(WorkCallback w, CompleteCallback c, ReportCallback p = null)
        {
            // init bg
            if (bg == null)
            {
                int alarmCounter = 0;
                var myTimer = new Timer((s) =>
                {
                    alarmCounter++;
                });
                bg = new BackgroundWorker()
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true,
                };
                bg.DoWork += (object sender, DoWorkEventArgs e) =>
                {
                    bg.ReportProgress(0);

                    // start work
                    var param = (BgParam)e.Argument;
                    param.r = param.w(e.Argument);
                    e.Result = param;
                };
                bg.ProgressChanged += (s, e) =>
                {
                    var param = e.UserState as BgParam;
                    if (param != null)
                    {
                        param.p(param.s);
                    }
                    else
                    {
                        // start timer
                        myTimer.Change(1000, -1);
                        alarmCounter = 0;
                    }
                };
                bg.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
                {
                    // stop timer & pogress
                    myTimer.Dispose();

                    // show result
                    var param = (BgParam)e.Result;
                    param.c(param.r);
                };
            }

            // check status
            if (bg.IsBusy) { return false; }

            // start detect in bg
            var paramObj = new BgParam { w = w, c = c, p = p };
            sectionParam = paramObj;
            bg.RunWorkerAsync(paramObj);
            return true;
        }

        public void Notify(object arg)
        {
            sectionParam.s = arg;
            bg.ReportProgress(1, sectionParam);
        }

        public class PrepareState : IBgWorkState
        {
            public System.Collections.Generic.List<IBgWorkElement> lst;
        }
        public class ExecuteState : IBgWorkState
        {
        }
        public class DoneState : IBgWorkState
        {

        }
        public bool BgExecute(IBgSection section)
        {
            object w(object arg)
            {
                var ps = section.Prepare();
                section.N(ps);
                foreach (var e in ps.lst)
                {
                    var ret = section.W(e);
                    section.N(ret);
                }
                return new DoneState();
            }
            void c(object result)
            {
                section.C((DoneState)result);
            }
            void p(object obj)
            {
                section.P((IBgWorkState)obj);
            }
            return BgExecute(w, c, p);
        }
        public interface IBgWorkState { }
        public interface IBgWorkInput { }
        public interface IBgWorkElement { }
        public interface IBgSection
        {
            ExecuteState W(IBgWorkElement o);
            PrepareState Prepare();
            void C(IBgWorkState o);
            void P(IBgWorkState o);
            void N(IBgWorkState o);
        }
    }
}
