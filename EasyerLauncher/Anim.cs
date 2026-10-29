using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>缓动曲线。</summary>
    public enum Ease
    {
        Linear,
        OutQuad,
        OutCubic,
        OutQuart,
        OutQuint,
        InOutCubic,
        OutBack
    }

    /// <summary>
    /// 全局动画泵：所有动画共用一个高精度计时器，按真实时间推进（帧率无关）。
    /// 旧实现每个控件各开一个 System.Windows.Forms.Timer，下载页 900 张卡片 = 900 个系统定时器，
    /// 既吃句柄又让 WM_TIMER 消息排队，这才是"卡钝"的根源。
    /// </summary>
    public static class Anim
    {
        public sealed class Handle
        {
            internal volatile bool Alive = true;
            public bool Running => Alive;
            public void Stop() { Alive = false; }
        }

        private sealed class Job
        {
            public Handle H;
            public double StartMs;
            public double DurMs;              // >0 定时型；<=0 持续型
            public Ease Ease;
            public Action<float> Step;
            public Func<double, bool> Tick;   // 持续型：返回 false 结束
            public Action Done;
            public Control Owner;
        }

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern int timeBeginPeriod(int ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern int timeEndPeriod(int ms);

        private static readonly List<Job> Jobs = new List<Job>();
        private static readonly List<Job> Graveyard = new List<Job>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static System.Windows.Forms.Timer _timer;
        private static bool _hiRes;
        private static double _lastTickMs = -100;

        /// <summary>定时动画：ms 毫秒内把 t 从 0 推到 1（已缓动）。</summary>
        public static Handle Run(double ms, Ease ease, Action<float> step, Action done = null, Control owner = null)
        {
            var h = new Handle();
            var j = new Job
            {
                H = h,
                StartMs = Clock.Elapsed.TotalMilliseconds,
                DurMs = ms < 1 ? 1 : ms,
                Ease = ease,
                Step = step,
                Done = done,
                Owner = owner
            };
            Jobs.Add(j);
            StartPump();
            // 立刻推一帧，避免"点了没反应"的延迟感
            step?.Invoke(EaseVal(ease, 0f));
            return h;
        }

        /// <summary>持续型动画：每帧调用 tick(dt 秒)，返回 false 自动结束（用于悬停渐变这类趋近动画）。</summary>
        public static Handle Loop(Func<double, bool> tick, Control owner = null)
        {
            var h = new Handle();
            var j = new Job
            {
                H = h,
                StartMs = Clock.Elapsed.TotalMilliseconds,
                DurMs = 0,
                Tick = tick,
                Owner = owner
            };
            Jobs.Add(j);
            StartPump();
            return h;
        }

        public static float EaseVal(Ease e, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            switch (e)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.OutCubic: return 1f - (float)Math.Pow(1f - t, 3);
                case Ease.OutQuart: return 1f - (float)Math.Pow(1f - t, 4);
                case Ease.OutQuint: return 1f - (float)Math.Pow(1f - t, 5);
                case Ease.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                case Ease.OutBack:
                    {
                        const float c1 = 1.70158f, c3 = c1 + 1f;
                        float p = t - 1f;
                        return 1f + c3 * p * p * p + c1 * p * p;
                    }
                default: return t;
            }
        }

        private static void StartPump()
        {
            if (!_hiRes)
            {
                // 把系统定时器分辨率提到 1ms，WM_TIMER 才能稳定跑到 ~60fps（默认只有 15.6ms）
                try { timeBeginPeriod(1); _hiRes = true; } catch { }
            }
            if (_timer == null)
            {
                _timer = new System.Windows.Forms.Timer { Interval = 8 };
                _timer.Tick += OnTick;
            }
            if (!_timer.Enabled) _timer.Start();
        }

        private static void OnTick(object sender, EventArgs e)
        {
            double now = Clock.Elapsed.TotalMilliseconds;
            if (now - _lastTickMs < 4.0) return;   // 限帧，防止计时器抖动时重复绘制
            _lastTickMs = now;

            Graveyard.Clear();
            for (int i = 0; i < Jobs.Count; i++)
            {
                var j = Jobs[i];
                if (!j.H.Alive || (j.Owner != null && j.Owner.IsDisposed))
                {
                    Graveyard.Add(j);
                    continue;
                }

                if (j.DurMs > 0)
                {
                    double p = (now - j.StartMs) / j.DurMs;
                    if (p > 1) p = 1;
                    try { j.Step?.Invoke(EaseVal(j.Ease, (float)p)); }
                    catch { j.H.Alive = false; }
                    if (p >= 1 || !j.H.Alive) Graveyard.Add(j);
                }
                else
                {
                    double dt = (now - j.StartMs) / 1000.0;
                    j.StartMs = now;
                    if (dt > 0.12) dt = 0.12;      // 掉帧时不跳变
                    bool more;
                    try { more = j.Tick(dt); }
                    catch { more = false; }
                    if (!more) Graveyard.Add(j);
                }
            }

            if (Graveyard.Count > 0)
            {
                var finished = new List<Job>(Graveyard.Count);
                foreach (var j in Graveyard)
                {
                    Jobs.Remove(j);
                    if (j.H.Alive && j.Done != null) finished.Add(j);
                }
                // 回调放在移除之后跑：Done 里可能再注册新动画
                foreach (var j in finished)
                {
                    try { j.Done(); } catch { }
                }
            }

            if (Jobs.Count == 0)
            {
                _timer.Stop();
                _lastTickMs = -100;
            }
        }

        /// <summary>窗口关闭时收尾，别让 timeBeginPeriod 泄漏。</summary>
        public static void Shutdown()
        {
            try { _timer?.Stop(); } catch { }
            Jobs.Clear();
            if (_hiRes)
            {
                try { timeEndPeriod(1); } catch { }
                _hiRes = false;
            }
        }
    }
}
