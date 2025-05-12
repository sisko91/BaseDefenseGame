
using System.Collections.Generic;
using Godot;

namespace Gurdy {

    // Holds timing data for a block of code that's being evaluated.
    public struct TimingData
    {
        public string Name;
        public float StartTimeMs;
        public float EndTimeMs;
        public List<TimingData> SubTimes;
        public float DurationMs => EndTimeMs - StartTimeMs;

        public bool Ended { get; private set; } = false;

        public TimingData(string name, float startTimeMs)
        {
            Name = name;
            StartTimeMs = startTimeMs;
            EndTimeMs = startTimeMs; // Default until ended
            SubTimes = [];
        }
        
        public void End()
        {
            if (Ended)
            {
                GD.PushWarning($"TimingData: `{Name}` has already ended but End() was called again.");
            }
            EndTimeMs = Time.GetTicksMsec();
            Ended = true;
        }
        public string PrettyPrint(int depth = 0)
        {
            string indent = new string('\t', depth); // depth=0 -> 0 tabs
            string line = $"{indent}{DurationMs}ms - {Name}";
            if (SubTimes != null)
            {
                foreach (var time in SubTimes)
                {
                    line += $"\n{time.PrettyPrint(depth+1)}";
                }
            }
            return line;
        }
    }
    
    // A simple named profiler that can be instantiated and used to measure timings easily. Designed to be passed to subroutines for capturing granular timings.
    public class SimpleProfiler
    {
        private TimingData root;
        private Stack<TimingData> stack;

        public SimpleProfiler(string rootName)
        {
            float now = Time.GetTicksMsec();
            root = new TimingData(rootName, now);
            stack = new Stack<TimingData>();
            stack.Push(root);
        }

        public void BeginBlock(string name)
        {
            if (root.Ended)
            {
                GD.PushWarning($"Profiler: `{root.Name} has already ended root block, cannot use to begin new blocks.");
                return;
            }
            float now = Time.GetTicksMsec();
            var newTiming = new TimingData(name, now);
            var current = stack.Pop();
            current.SubTimes.Add(newTiming);
            stack.Push(current); // re-push parent
            stack.Push(newTiming); // push new child as current
        }

        public void EndBlock()
        {
            if (root.Ended)
            {
                GD.PushWarning($"Profiler: `{root.Name} has already ended root block, cannot end another block.");
                return;
            }
            
            var finished = stack.Pop();
            finished.End();

            // Merge changes back to parent
            if (stack.TryPop(out var parent))
            {
                var subs = parent.SubTimes;
                subs[subs.Count - 1] = finished;
                stack.Push(parent);
            }
            else
            {
                root = finished;
            }
        }

        public string EndAndReport()
        {
            while (stack.Count > 1)
            {
                GD.PushWarning($"Profiler: `{root.Name}` was asked to produce a report while non-root block `{stack.Peek().Name}` was still in progress.");
                EndBlock();
            }
            return root.PrettyPrint();
        }
    }
}
