//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using Tap.Shared;
using OpenTap.Engine.UnitTests.TestTestSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ReflectionHelperTest
    {

        [Test]
        public void EnumerableTest()
        {
            int[] x = new[] { 1, 2, 3, 4 };
            Assert.IsFalse(x.IsLongerThan(4));
            Assert.IsTrue(x.IsLongerThan(3));
            Assert.IsFalse(x.IsLongerThan(400000));

            Assert.AreEqual(x.IndexWhen(y => y == 3), 2);
            int sum = 0;
            x.ForEach(y => sum += y);
            Assert.AreEqual(sum, 10);
        }

        [Test]
        public void ArrayAppendTest()
        {
            int[] items = {0, 1, 2, 3, 4};
            Sequence.Append(ref items, 5, 6);
            for (int i = 0; i < 7; i++)
                Assert.AreEqual(i, items[i]);
        }

        [Test]
        public void PairWiseTest()
        {
            
            var a = Enumerable.Range(0, 5);
            var b = Enumerable.Range(1, 6);
            var p2 = a.Pairwise(b);
            Assert.AreEqual(5, p2.Count());
            foreach (var pair in p2)
            {
                Assert.IsTrue(pair.Item1 == pair.Item2 - 1);
            }
        }

        [Test]
        public void PermutationsTest()
        {
            var source = new [] { new [] { 1, 2 }, new [] { 3, 4, 5 }, new []{6, 7} };
            var perms = source.Permutations().ToArray();
            var ordered = perms.OrderBy(x => x[1])
                .ToArray()
                .OrderBy(x => x[0])
                .ToArray();
            int index = 0;
            for (int i = 0; i < source[0].Length; i++)
            {
                for (int j = 0; j < source[1].Length; j++)
                {
                    for (int k = 0; k < source[2].Length; k++)
                    {
                        Assert.IsTrue(source[0][i] == ordered[index][0]);
                        Assert.IsTrue(source[1][j] == ordered[index][1]);
                        Assert.IsTrue(source[2][k] == ordered[index][2]);
                        index++;
                    }
                }
            }

            int len = 1;
            foreach (var array in source)
                len *= array.Length;
            Assert.AreEqual(len, perms.Length);
            
            // edge case 1: An empty array yields no results.
            var sourceEdgeCase1 = new [] { new [] { 1, 2 }, new int[] { }, new []{6, 7} };
            var permsEdgeCase1 = sourceEdgeCase1.Permutations().ToArray();
            Assert.AreEqual(0, permsEdgeCase1.Length);

            // edge case 2: Nothing yields one empty array.
            // This is the correct behavior, because there is exactly one valid permutation.
            // this also makes sense if you try to calculate the number of permutations.
            var sourceEdgeCase2 = new int[][] {}; 
            var permsEdgeCase2 = sourceEdgeCase2.Permutations().ToArray();
            Assert.AreEqual(1, permsEdgeCase2.Length);
            Assert.AreEqual(0, permsEdgeCase2[0].Length);


        }

        [Test]
        public void MemoryMappedApiTest()
        {
            if(OpenTap.OperatingSystem.Current != OpenTap.OperatingSystem.Windows) 
                return;  // this feature is currently not supported on Linux.
            var api = new MemoryMappedApi();
            var strings = new[] { "asd", "cxze", "" };
            foreach(var str in strings)
                api.Write(str);
            
            api.Persist();
            var api2 = new MemoryMappedApi(api.Name);
            foreach (var str in strings)
                Assert.AreEqual(api2.Read<string>(), str);
                
            api2.ReadRewind();
            foreach (var str in strings)
                Assert.AreEqual(api2.Read<string>(), str);

            api = new MemoryMappedApi();
            var array = new byte[] { 1, 2, 3, 4, 5, 6 };
            api.Write(array);

            var bf = new BinaryFormatter();
            var memstr = new MemoryStream();
            string[] strings2 = new[] { "asd", "", null };
#pragma warning disable SYSLIB0011 // Type or member is obsolete
            bf.Serialize(memstr, strings2);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
            api.Write(memstr.ToArray());

            var array2 = new int[] { 1, 2, 3, 4, 5, 6 };
            api.Write(array2);
            
            api.Persist();
            api2 = new MemoryMappedApi(api.Name);
            var thedata = api2.Read<byte[]>();
            Assert.IsTrue(array.SequenceEqual(thedata));
            var stream = api2.ReadStream();
#pragma warning disable SYSLIB0011 // Type or member is obsolete
            var strings3 = (string[])bf.Deserialize(stream);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
            strings2.SequenceEqual(strings3);
            var array3 = api2.Read<int[]>();
            Assert.IsTrue(array2.SequenceEqual(array3));
        }

        [Test]
        [Platform(Exclude="Unix,Linux,MacOsX")]
        public void TimeoutOperationTest()
        {
            var sem = new System.Threading.Semaphore(0, 1);
            TapThread.Start(() => sem.Release());
            sem.WaitOne();
            {
                bool calledAction = false;
                var operation = TimeoutOperation.Create(TimeSpan.FromMilliseconds(1), () =>
                {
                    calledAction = true;
                    sem.Release();
                });
                sem.WaitOne();
                Assert.IsTrue(calledAction);
            }
            {
                bool calledAction = false;
                var operation = TimeoutOperation.Create(TimeSpan.FromMilliseconds(50), () => calledAction = true);
                operation.Dispose();
                Assert.IsFalse(calledAction);
            }
            {
                bool calledAction = false;
                var operation = TimeoutOperation.Create(TimeSpan.FromMilliseconds(1), () =>
                {
                    calledAction = true;
                });
                operation.Dispose();
                Assert.IsFalse(calledAction);
            }
            {
                bool calledAction = false;
                var operation = TimeoutOperation.Create(TimeSpan.FromMilliseconds(1), () => calledAction = true);
                Assert.IsFalse(calledAction);
                operation.Dispose();
            }
        }

        static int getMurMur(int i)
        {
            switch (i)
            {
                case 0: return 1036651960;
                case 1: return -108976867;
                case 2: return -888838031;
                case 3: return 1867787361;
                case 4: return 531708635;
                case 5: return -687432098;
                case 6: return 182881051;
                case 7: return 1461746781;
                case 8: return 619631658;
                case 9: return 2054570891;
                default:
                    throw new InvalidOperationException();

            }
        }

        [Test]
        public void TestMurmur3()
        {
            var rnd = new Random(100);
            byte[] buffer = new byte[100];
            

            for (int i = 0; i < 10; i++){

                rnd.NextBytes(buffer);
                var hello = MurMurHash3.Hash(buffer);
                Assert.AreEqual(getMurMur(i), hello);
            }

            var test2 = MurMurHash3.Hash("H3wlo World4!!!!");
            Assert.AreEqual(1251584510, test2);

        }

        public class Things
        {
            public Things[] Sub;
            public int Value;
        }

        [Test]
        public void FlattenHeirarchyTest()
        {
            var a = new Things() { Value = 1, Sub = new[] { new Things { Value = 2 }, new Things { Value = 4 } } };
            Assert.AreEqual(7 + 2, Utils.FlattenHeirarchy(new[] { a, a.Sub[0] }, x => x.Sub).Select(x => x.Value).Sum());
            HashSet<Things> set = new HashSet<Things>();
            Utils.FlattenHeirarchyInto(new[] { a, a.Sub[0] }, x => x.Sub, set);
            Assert.AreEqual(7, set.Sum(x => x.Value));
        }

        class BaseTest
        {
            public virtual void Test()
            {

            }
        }

        class BaseTest2 : BaseTest
        {

        }

        class BaseTest3 : BaseTest
        {
            public override void Test()
            {
                base.Test();
            }
        }


        [Test]
        public void OverriderTest()
        {
            //
            // this test demonstrates the behavior that can be used to test if a 
            // method is overridden or not.
            //
            // This behavior most work otherwise PrePlanRun/PostPlanRun optimizations wont work.
            //
            MethodInfo baseclaseMethod = typeof(BaseTest).GetMethod("Test");
            MethodInfo inheritedButNotOverridden = typeof(BaseTest2).GetMethod("Test");
            MethodInfo inheritedAndOverridden = typeof(BaseTest3).GetMethod("Test");
            Assert.AreEqual(baseclaseMethod.MethodHandle.Value, inheritedButNotOverridden.MethodHandle.Value);
            Assert.AreNotEqual(baseclaseMethod.MethodHandle.Value, inheritedAndOverridden.MethodHandle.Value);


            // delayStep does not override PrePlanRun or PostPlanRun
            var delay = new DelayStep();
            Assert.IsFalse(delay.PrePostPlanRunUsed);

            // TimingTestStep does..
            var timing = new TimingTestStep();
            Assert.IsTrue(timing.PrePostPlanRunUsed);
        }

        [Test]
        public void NaturalSortTest()
        {
            // first entry is a null.
            // string.Compare always puts null first.
            var strnat = ",,-1042.5,-1042,-1032,-100,-100A,-100B,-100CDE,-100D,-50A,-50B,-50C,-50K,0ASD,0BSD,0.1,0.2,0.3,10X,10Y,100.3210X,100.3210Y,+100.5X,+100.5Y,A,A10,A20B,A30B,A40B,B,C,D";
            var strnats = strnat.Split(new[] { "," },StringSplitOptions.None).ToList();
            strnats[0] = null; // insert null in place of the first ''.
            strnats.Sort();
            for(int i = 0; i < 10; i++){
                strnats.Shuffle();

                strnats.Sort(Utils.NaturalCompare);
                var joined = string.Join(",", strnats);
                Assert.AreEqual(joined, strnat);
            }

        }

        [Test]
        public void RemoveIfTest()
        {
            var rnd = new Random();
            var values = Enumerable.Repeat(0, 1000).Select(x => rnd.NextDouble()).ToList();
            Assert.IsTrue(values.IndexWhen(x => x < 0.5) != -1);
            values.RemoveIf(x => x < 0.5); // remove all values < 0.5.
            Assert.IsTrue(values.IndexWhen(x => x < 0.5) == -1);
        }

        [Test]
        public void TestCheckOperatingSystem()
        {
            // we can use RuntimeInformation to check that our OperatingSystem implementation works.
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.AreEqual(OperatingSystem.Windows, OperatingSystem.Current);
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                Assert.AreEqual(OperatingSystem.MacOS, OperatingSystem.Current);
            }
            else
            {
                Assert.AreEqual(OperatingSystem.Linux, OperatingSystem.Current);
            }
        }

        [Test]
        public void TestDirSearch()
        {
            var opentapdir = Path.GetDirectoryName(typeof(TestPlan).Assembly.Location);
            var opentapfile = Path.GetFileName(typeof(TestPlan).Assembly.Location);
            var files = PathUtils.IterateDirectories(opentapdir, "*.dll", SearchOption.AllDirectories).ToArray();
            var opentapdll = files.FirstOrDefault(x => Path.GetFileName(x) == opentapfile);
            Assert.IsNotNull(opentapdll);
        }

        [Test]
        public void MemorizerValidationTest()
        {
            int globalData = 1;
            var mem = new Memorizer<int, string>(i => (i * globalData).ToString())
            {
                Validator = x => x + globalData
            };
            
            Assert.AreEqual("4", mem[4]);
            globalData = 2;
            Assert.AreEqual("8", mem[4]);
            globalData = 3;
            Assert.AreEqual("12", mem[4]);
            globalData = 4;
            Assert.AreEqual("16", mem[4]);
        }

        [Flags]
        enum TestFlags
        {
            None = 0,
            Value1 = 1,
            [Display("Value 2")]
            Value2 = 2,
            [Display("Value 4")]
            Value4 = 4
            
        }

        const EngineSettings.AbortTestPlanType blankAbortType = 0;
        [TestCase(EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, "Break On Fail | Break On Error")]
        [TestCase(EngineSettings.AbortTestPlanType.Step_Error, "Break On Error")]
        [TestCase(EngineSettings.AbortTestPlanType.Step_Fail, "Break On Fail")]
        [TestCase(blankAbortType, "")]
        [TestCase(Verdict.Pass, "Pass")]
        [TestCase(Verdict.NotSet, "Not Set")]
        
        [TestCase(TestFlags.None, "None")]
        [TestCase(TestFlags.None | TestFlags.Value1, "Value1")]
        [TestCase(TestFlags.Value2 | TestFlags.Value1, "Value1 | Value 2")]
        [TestCase(TestFlags.Value2 | TestFlags.Value1 | TestFlags.Value4, "Value1 | Value 2 | Value 4")]
        public void TestEnumToString(Enum testValue, string expectedString)
        {
            var actualString = Utils.EnumToReadableString(testValue);
            Assert.AreEqual(expectedString, actualString);
        }

        [Test]
        public void TestInvalidEnumToString()
        {
            TestEnumToString((Verdict) 111, "111");
            // 1 and 2 are included in 111, so actually these flags are set.
            TestEnumToString((EngineSettings.AbortTestPlanType) 111, "Break On Fail | Break On Error | Break On Inconclusive | Break On Pass");
        }


        [TestCase(1000, "1.00 kB")]
        [TestCase(0, "0 B")]
        [TestCase(110, "110 B")]
        [TestCase(1500, "1.50 kB")]
        [TestCase(15000, "15.00 kB")]
        [TestCase(150000, "150.00 kB")]
        [TestCase(1500000, "1.50 MB")]
        [TestCase(1000000, "1.00 MB")]
        [TestCase(15500000, "15.50 MB")]
        [TestCase(155550000, "155.55 MB")]
        [TestCase(1550000000, "1.55 GB")]
        [TestCase(2000000000, "2.00 GB")]
        [TestCase(20500000000, "20.50 GB")]
        public void TestBytesToReadable(long number, string expected)
        {
            var str = Utils.BytesToReadable(number);
            Assert.AreEqual(expected, str);
        }
        
        [Test]
        public void TrySelectUnwrapTest()
        {
            var x = new int[] { 1, 0, 3, 0 };
            int exceptionsCaught = 0;
            var inv1 = x.TrySelect(x => 1 / x, (e, x) => exceptionsCaught++).ToArray();
            var inv2 = x.TrySelect(x => 1 / x, e => exceptionsCaught++).ToArray();
            Assert.IsTrue(inv1.SequenceEqual(new[] { 1, 0 }));
            Assert.IsTrue(inv1.SequenceEqual(inv2));
            Assert.AreEqual(4, exceptionsCaught);
        }
    }
}
