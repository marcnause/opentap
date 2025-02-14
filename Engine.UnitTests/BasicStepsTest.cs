using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class BasicStepsTest
    {
        [TestCase(true, Verdict.Aborted, null)]
        [TestCase(false, Verdict.Error, null)]
        [TestCase(false, Verdict.Pass, Verdict.Pass)]
        public void TimeGuardStepTest(bool stopOnError, Verdict expectedVerdict, Verdict? verdictOnAbort)
        {
            var plan = new TestPlan();
            var guard = new TimeGuardStep {StopOnTimeout = stopOnError, Timeout = 0.05};
            if (verdictOnAbort != null)
                guard.TimeoutVerdict = verdictOnAbort.Value;
            
            // if this delay step runs to completion, the verdict of the test plan will be NotSet, failing the final assertion.
            var delay = new DelayStep {DelaySecs = 120};
            plan.ChildTestSteps.Add(guard);
            guard.ChildTestSteps.Add(delay);
            var run = plan.Execute();
            
            Assert.AreEqual(expectedVerdict, run.Verdict);
        }


        class PassThirdTime : TestStep
        {
            public int Iterations = 0;
            public override void PrePlanRun()
            {
                Iterations = 0;
                base.PrePlanRun();
            }

            public override void Run()
            {
                Iterations += 1;
                if (Iterations < 3)
                {
                    UpgradeVerdict(Verdict.Fail);
                }
                UpgradeVerdict(Verdict.Pass);
            }
        }
        
        [Test]
        [Pairwise]
        public void RepeatUntilPass([Values(true, false)] bool retry)
        {
            var step = new PassThirdTime();
            BreakConditionProperty.SetBreakCondition(step, BreakCondition.BreakOnFail);
            
            var rpt = new RepeatStep()
            {
                Action =  RepeatStep.RepeatStepAction.Until,
                TargetStep = step,
                TargetVerdict = Verdict.Pass,
                Retry = retry
            };
            rpt.ChildTestSteps.Add(step);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);

            var run = plan.Execute();

            if (retry)
            {
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.AreEqual(3, step.Iterations);
            }
            else
            {
                // break condition reached -> Error verdict.
                Assert.AreEqual(Verdict.Fail, run.Verdict);
                Assert.AreEqual(1, step.Iterations);
            }
        }
        
        [Test]
        public void RepeatUntilPass2()
        {
            var step = new PassThirdTime();
            var rpt = new RepeatStep
            {
                Action =  RepeatStep.RepeatStepAction.Until,
                TargetStep = step,
                TargetVerdict = Verdict.Pass,
                ClearVerdict = true,
                MaxCount = new Enabled<uint>{IsEnabled = true, Value = 5}
            };
            rpt.ChildTestSteps.Add(step);
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);
            
            var run = plan.Execute();

            Assert.AreEqual(Verdict.Pass, run.Verdict);
            Assert.AreEqual(3, step.Iterations);
        }
        
        
        // These two cases are technically equivalent.
        [Test]
        [TestCase(Verdict.Fail, RepeatStep.RepeatStepAction.While)]
        [TestCase(Verdict.Pass, RepeatStep.RepeatStepAction.Until)]
        public void RepeatWhileError(Verdict targetVerdict, RepeatStep.RepeatStepAction action)
        {
            var step = new PassThirdTime();
            BreakConditionProperty.SetBreakCondition(step, BreakCondition.BreakOnFail);
            
            var rpt = new RepeatStep()
            {
                Action =  action,
                TargetVerdict = targetVerdict,
                Retry = true
            };
            rpt.TargetStep = rpt; // target self. The Repeat Loop will inherit the verdict.
            rpt.ChildTestSteps.Add(step);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(rpt);

            var run = plan.Execute();

            Assert.AreEqual(Verdict.Pass, run.Verdict); 
            Assert.AreEqual(3, step.Iterations);
        }

        [Test]
        public void ScpiRegexStepValidation()
        {
            ScpiInstrument instr = new ScpiDummyInstrument();
            instr.Rules.Clear(); // skip validation on the instrument itself.
            SCPIRegexStep scpiRegex = new SCPIRegexStep
            {
                Instrument = instr,
                Query = "SYST:CHAN:MOD? (@1,2)", // query with arguments.
                Action = SCPIAction.Query
            };
            Assert.IsTrue(string.IsNullOrEmpty(scpiRegex.Error));
            scpiRegex.Query = "SYST:CHAN:MOD"; // Not a valid query!
            Assert.IsFalse(string.IsNullOrEmpty(scpiRegex.Error));
            scpiRegex.Action = SCPIAction.Command; // it is a valid command though.
            Assert.IsTrue(string.IsNullOrEmpty(scpiRegex.Error));
        }

        class SimpleResultTest2 : TestStep 
        {
            public override void Run()
            {
                Results.Publish("Test", new List<string> { "X", "Y" }, 1.0, 2.0);
            }
        }
        
        [Test]
        public void RepeatCheckResultsHasIterations()
        {
            var pushResult1 = new SimpleResultTest2();
            var pushResult2 = new SimpleResultTest2();
            
            var repeatStep = new RepeatStep
            {
                Action =  RepeatStep.RepeatStepAction.Fixed_Count,
                Count = 100
            };
            
            var collectEverythingListener = new RecordAllResultListener();
            
            repeatStep.ChildTestSteps.Add(pushResult1);
            repeatStep.ChildTestSteps.Add(pushResult2);

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(repeatStep);

            plan.Execute(new IResultListener[]{collectEverythingListener});
            
            // verify that there are 200 distinct result tables (from 200 different test plan runs)
            // 200 = repeatStep.Count * 2 (pushResult1 and pushResult2).
            Assert.AreEqual(200, collectEverythingListener.Results.Count);
            
            // verify that each result table came from a different step run
            Assert.AreEqual(200, collectEverythingListener.ResultTableGuids.Distinct().Count());
        }
    }
}