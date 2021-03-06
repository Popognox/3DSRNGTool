﻿using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool.Core
{
    internal static class RNGPool
    {
        // Queue
        private static uint[] RandList;
        private static ulong[] RandList64;
        private static PRNGState[] RNGStateStr;
        private static int Tail, BufferSize, Pointer;
        private static int Head => Tail == BufferSize - 1 ? 0 : Tail + 1;
        public static uint getrand => RandList[++Pointer >= BufferSize ? Pointer = 0 : Pointer];
        public static ulong getrand64 => RandList64[++Pointer >= BufferSize ? Pointer = 0 : Pointer];

        public static int index
        {
            get
            {
                int i = Pointer - Tail;
                if (i < 0)
                    i += BufferSize;
                return i;
            }
            private set
            {
                Pointer = Tail + value;
                if (Pointer >= BufferSize)
                    Pointer -= BufferSize;
            }
        }

        public static void Advance(int d)
        {
            Pointer += d;
            if (Pointer >= BufferSize)
                Pointer -= BufferSize;
        }

        public static void Clear()
        {
            RandList = null;
            RandList64 = null;
            RNGStateStr = null;
            tinystatus = null;
            timeline = null;
            horde = null;
        }

        public static void CreateBuffer(int buffersize, IRNG rng)
        {
            BufferSize = buffersize;
            Tail = buffersize - 1;
            if (rng is IRNG64 rng64)
            {
                RandList64 = new ulong[buffersize];
                for (int i = 0; i < buffersize; i++)
                    RandList64[i] = rng64.Nextulong();
                return;
            }
            RandList = new uint[buffersize];
            RNGStateStr = new PRNGState[buffersize];
            for (int i = 0; i < buffersize; i++)
            {
                RNGStateStr[i] = (rng as IRNGState)?.CurrentState();
                RandList[i] = rng.Nextuint();
            }
        }

        public static void AddNext(IRNG rng)
        {
            if (rng is IRNG64 rng64)
            {
                RandList64[Head] = rng64.Nextulong();
                if (++Tail == BufferSize) Tail = 0;
                return;
            }
            RNGStateStr[Head] = (rng as IRNGState)?.CurrentState();
            RandList[Head] = rng.Nextuint();
            if (++Tail == BufferSize) Tail = 0;
        }

        public static bool Considerdelay;
        public static int DelayTime;

        public static Pokemon PM;
        public static bool IsMainRNGEgg;
        public static IGenerator igenerator;

        public static RNGResult Generate6()
        {
            index = Considerdelay ? DelayTime : 0;
            Advance(1);
            var result = igenerator.Generate() as Result6;
            result.RandNum = RandList[Head];
            result.Status = RNGStateStr[Head];
            return result;
        }

        public static RNGResult[] GenerateHorde6()
        {
            index = Considerdelay ? DelayTime : 0;
            Advance(1);
            var results = (igenerator as Wild6).Generate_Horde(horde);
            foreach (var result in results)
            {
                result.RandNum = RandList[Head];
                result.Status = RNGStateStr[Head];
            }
            return results;
        }

        public static RNGResult GenerateEgg6()
        {
            index = Considerdelay ? DelayTime : 0;
            Advance(1);
            if (IsMainRNGEgg) Egg6.MainRNGPID = getrand; // Previous Egg PID
            var result = GenerateAnEgg6(new uint[] { getrand, getrand }); // New Egg Seed
            result.RandNum = RandList[Head];
            result.Status = RNGStateStr[Head];
            return result;
        }

        public static ResultE6 GenerateAnEgg6(uint[] key)
        {
            Egg6.ReSeed(key);
            var result = igenerator.Generate() as ResultE6;
            result.EggSeed = key[0] | ((ulong)key[1] << 32);
            return result;
        }

        public static RNGResult Generate7()
        {
            Pointer = Tail;
            int frameshift = getframeshift();
            var result = igenerator.Generate() as Result7;
            result.RandNum = RandList64[Head];
            result.FrameDelayUsed = frameshift;
            return result;
        }

        public static RNGResult GenerateEgg7()
        {
            Pointer = Tail;
            var result = igenerator.Generate() as ResultE7;
            result.RandNum = RandList[Head];
            result.Status = RNGStateStr[Head];
            result.FramesUsed = index;
            return result;
        }
        #region Gen6 Tiny Timeline

        public static TinyStatus tinystatus;
        public static TinyTimeline timeline;
        public static HordeResults horde;

        public static void time_elapse6(int i) => tinystatus.time_elapse(i); // Only Tiny Advance according to timeline. i.e. MT not advance
        public static void AdvanceTiny() => tinystatus.Tinyrng.Next();

        #endregion

        #region Gen7 Time keeping

        public static bool raining, phase;
        public static byte modelnumber;
        public static int[] remain_frame;
        
        public static bool IsSolgaleo, IsLunala, IsExeggutor;
        public static int PreHoneyCorrection;

        public static void ResetModelStatus()
        {
            remain_frame = new int[modelnumber];
            phase = false;
        }

        public static void CopyStatus(ModelStatus st)
        {
            modelnumber = st.Modelnumber;
            remain_frame = (int[])st.remain_frame.Clone();
            phase = st.phase;
        }

        public static void time_elapse7(int n)
        {
            for (int totalframe = 0; totalframe < n; totalframe++)
            {
                for (int i = 0; i < modelnumber; i++)
                {
                    if (remain_frame[i] > 1)                       //Cooldown 2nd part
                    {
                        remain_frame[i]--;
                        continue;
                    }
                    if (remain_frame[i] < 0)                       //Cooldown 1st part
                    {
                        if (++remain_frame[i] == 0)                //Blinking
                            remain_frame[i] = (int)(getrand64 % 3) == 0 ? 36 : 30;
                        continue;
                    }
                    if ((int)(getrand64 & 0x7F) == 0)              //Not Blinking
                        remain_frame[i] = -5;
                }
                if (raining && (phase = !phase))
                    Advance(2);
            }
        }

        //model # changes when screen turns black
        private readonly static int[] order = { 0, 1, 2, 5, 6 };
        private static void SolLunaRearrange()
        {
            modelnumber = 5;//2 guys offline...
            for (int i = 0; i < 5; i++)
                remain_frame[i] = remain_frame[order[i]];
        }

        //Another type of change (Lillie)
        private static void ExeggutorRearrange()
        {
            modelnumber = 2;
            int tmp = remain_frame[0];
            remain_frame = new int[2];
            remain_frame[0] = tmp;
        }
        
        public static void NormalDelay7() => time_elapse7(DelayTime);

        public static void StationaryDelay7()
        {
            if (IsSolgaleo || IsLunala)
            {
                int crydelay = IsSolgaleo ? 79 : 76;
                time_elapse7(DelayTime - crydelay - 19);
                if (modelnumber == 7) SolLunaRearrange();
                time_elapse7(19);
                Advance(1);     //Cry Inside Time Delay
                time_elapse7(crydelay);
                return;
            }
            if (IsExeggutor)
            {
                time_elapse7(1);
                if (modelnumber == 1) ExeggutorRearrange();
                time_elapse7(42);
                Advance(1);    //Cry Inside Time Delay
                time_elapse7(DelayTime - 43);
                return;
            }
            NormalDelay7();
        }

        public static void WildDelay7()
        {
            NormalDelay7();
            ResetModelStatus();
            if (raining) Advance(2);
            time_elapse7(1);              //Blink process also occurs when loading map
            Advance(PreHoneyCorrection - modelnumber);  //Pre-HoneyCorrection
            time_elapse7(93);
        }

        private static int getframeshift()
        {
            if (Considerdelay)
            {
                time_elapse7(2); // Button pressing delay
                igenerator.Delay();
            }
            else
                ResetModelStatus();
            return index;
        }
        #endregion
    }
}
