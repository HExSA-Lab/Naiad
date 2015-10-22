/*
 * Naiad ver. 0.6
 * Copyright (c) Microsoft Corporation
 * All rights reserved. 
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0 
 *
 * THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES OR
 * CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT
 * LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR
 * A PARTICULAR PURPOSE, MERCHANTABLITY OR NON-INFRINGEMENT.
 *
 * See the Apache Version 2.0 License for specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.Naiad.Dataflow.Channels;
using Microsoft.Research.Naiad.Serialization;
using Microsoft.Research.Naiad.DataStructures;
using Microsoft.Research.Naiad.Runtime.FaultTolerance;

using Microsoft.Research.Naiad.Diagnostics;

namespace Microsoft.Research.Naiad.Frameworks.DifferentialDataflow.CollectionTrace
{
    /// <summary>
    /// Interface for a collection trace, which is a mapping from key indices (of type int)
    /// to times (interned, of type int) to records (of time R), to weights (of type int).
    /// 
    /// Typically the key indices are obtained from a Operator.KeyIndices structure, which
    /// indicates which offsets in some internal data structure are used for the unprocessed,
    /// processed, etc. regions of the collection trace.
    /// 
    /// Typically the time indices are obtained from a LatticeInternTable object associated with
    /// the enclosing operator.
    /// </summary>
    /// <typeparam name="R">Record type</typeparam>
    internal interface CollectionTrace<R>
        where R : IEquatable<R>
    {
        /// <summary>
        /// Introduces the given record with the given weight at a particular (key, time).
        /// 
        /// After executing this method, the keyIndex may have changed.
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        /// <param name="record">The record to introduce.</param>
        /// <param name="weight">The weight of the record to introduce.</param>
        /// <param name="timeIndex">The index for the time to update.</param>
        void Introduce(ref int keyIndex, R record, Int64 weight, int timeIndex);

        /// <summary>
        /// Introduces a batch of records from a source region in this collection trace (typically
        /// corresponding to an unprocessed trace or a workspace) to a destination region.
        /// 
        /// After executing this method, the destKeyIndex and sourceKeyIndex may have changed.
        /// </summary>
        /// <param name="destkeyIndex">The index for the destination, which will be updated.</param>
        /// <param name="sourceKeyIndex">The index for the source for the the update.</param>
        /// <param name="delete">If true, this will free the storage associated with the sourceKeyIndex.</param>
        void IntroduceFrom(ref int destkeyIndex, ref int sourceKeyIndex, bool delete = true);

        /// <summary>
        /// Updates dA[t] to be the negation of all strictly prior differences, where A is the given
        /// key region.
        /// 
        /// After executing this method, the keyIndex may have changed.
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        /// <param name="timeIndex">The index for the time to update.</param>
        void SubtractStrictlyPriorDifferences(ref int keyIndex, int timeIndex);

        /// <summary>
        /// Sets the state associated with the given key to zero.
        /// 
        /// After executing this method, the keyIndex may have changed.
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        void ZeroState(ref int keyIndex);

        /// <summary>
        /// Returns true if the state associated with the given key is zero.
        /// 
        /// After executing this method, the keyIndex may have changed.
        /// </summary>
        /// <param name="keyIndex">The index for the key to interrogate.</param>
        /// <returns>True if the state associated with the given key is zero, otherwise false.</returns>
        bool IsZero(ref int keyIndex);

        /// <summary>
        /// Enumerates the entire collection for the given key at the given time, and stores it in the
        /// given list.
        /// </summary>
        /// <param name="keyIndex">The key to enumerate.</param>
        /// <param name="timeIndex">The time at which to enumerate the collection.</param>
        /// <param name="toFill">The list that will be populated with records in the collection.</param>
        void EnumerateCollectionAt(int keyIndex, int timeIndex, NaiadList<Weighted<R>> toFill);

        /// <summary>
        /// Enumerates the difference for the given key at the given time, and stores it in the given list.
        /// </summary>
        /// <param name="keyIndex">The key to enumerate.</param>
        /// <param name="timeIndex">The time at which to enumerate the difference.</param>
        /// <param name="toFill">The list that will be populated with records in the difference.</param>
        void EnumerateDifferenceAt(int keyIndex, int timeIndex, NaiadList<Weighted<R>> toFill);

        /// <summary>
        /// Enumerates the set of times at which the given key changes, and stores it in the given list.
        /// </summary>
        /// <param name="keyIndex">The key to enumerate.</param>
        /// <param name="timelist">The list that will be populated with the times at which the given key changes.</param>
        void EnumerateTimes(int keyIndex, NaiadList<int> timelist);

        /// <summary>
        /// Updates the state associated with the given key to reflect times that may have been advanced (with a view to compacting their state).
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        void EnsureStateIsCurrentWRTAdvancedTimes(ref int keyIndex);

        /// <summary>
        /// Set to true any time used by the key
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        /// <param name="usedTimes">An array of interned times that are used: the relevant element gets set for every time used after update</param>
        void MarkUsedTimes(int keyIndex, bool[] usedTimes);

        /// <summary>
        /// Updates the state associated with the given key to reflect a new compacted intern table
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        /// <param name="transferTime">The function that translates an old time index into a new time index.</param>
        void TransferTimesToNewInternTable(int keyIndex, int[] transferTime);

        /// <summary>
        /// Updates the state associated with the given key to remove all elements whose time does not match
        /// a predicate
        /// </summary>
        /// <param name="keyIndex">The index for the key to update.</param>
        /// <param name="keepTime">The predicate that indicates which times to keep.</param>
        void RemoveStateInTimes(ref int keyIndex, Func<int, bool> keepTime);

        /// <summary>
        /// Count all the entries is a given key at a given time
        /// </summary>
        /// <param name="keyIndex">the key to count</param>
        /// <param name="timeIndex">the time to count</param>
        /// <returns>the number of differences in the key at the time</returns>
        long CountDifferenceAt(int keyIndex, int timeIndex);

        /// <summary>
        /// Make sure time advancing works with a new intern table
        /// </summary>
        /// <param name="newLessThan">The new intern table's time comparison function.</param>
        /// <param name="newUpdateTime">The new intern table's time update function.</param>
        void InstallNewUpdateFunction(Func<int, int, bool> newLessThan, Func<int, int> newUpdateTime);

        /// <summary>
        /// Called when the collection trace is released. Currently unused.
        /// </summary>
        void Release();

        /// <summary>
        /// Applies eager compaction to the collection trace.
        /// </summary>
        void Compact();
    }

    internal interface CollectionTraceCheckpointable<R> : CollectionTrace<R>
        where R : IEquatable<R>
    {
        void Checkpoint(NaiadWriter writer);
        void Restore(NaiadReader reader);
    }

    /// <summary>
    /// Represents a collection trace for a collection that is constant with respect to time.
    /// </summary>
    /// <typeparam name="R">The type of records stored in this trace.</typeparam>
    internal class CollectionTraceImmutable<R>  : CollectionTraceCheckpointable<R>
        where R : IEquatable<R>
    {
        private class SpinedList<T>
        {
            public T[][] spine;
            public int Count;

            public void Add(T element)
            {
                if (spine[this.Count >> 16] == null)
                {
                    spine[this.Count >> 16] = new T[1 << 16];
                    //Console.Error.WriteLine("Allocated spine[{0}]", this.Count >> 16);
                }

                spine[this.Count >> 16][this.Count % (1 << 16)] = element;
                this.Count++;
            }
            public T ElementAt(int index)
            {
                return spine[index >> 16][index & 0xFFFF];
            }

            public SpinedList()
            {
                spine = new T[1 << 16][];
            }
        }

        bool debug = false;

        private List<int> heads;            // index in links of the last element with the associated key.
        private SpinedList<Microsoft.Research.Naiad.Pair<R, int>> links;   // chain of links forming linked lists for each key.

        //List<List<R>> list;

        private int[] offsets;
        private R[] data;

        public void Introduce(ref int keyIndex, R from, Int64 weight, int timeIndex)
        {
            if (heads != null)
            {
                if (keyIndex == 0)
                {
                    keyIndex = heads.Count;
                    heads.Add(-1);
                }

                if (weight < 0)
                    Logging.Error("Subtracting records from Immutable collection (Consolidate first?)");

                for (int i = 0; i < weight; i++)
                {
                    links.Add(new Microsoft.Research.Naiad.Pair<R, int>(from, heads[keyIndex]));
                    heads[keyIndex] = links.Count - 1;
                }
            }
            else
                Logging.Error("Adding records to Immutable collection after compacting it");
        }

        public void IntroduceFrom(ref int thisKeyIndex, ref int thatKeyIndex, bool delete = true)
        {
            if (thatKeyIndex == 0)
                return;

            if (heads != null)
            {
                if (thisKeyIndex == 0 && delete)
                {
                    thisKeyIndex = thatKeyIndex;
                    thatKeyIndex = 0;
                }
                else
                {
                    int index = heads[thatKeyIndex];
                    while (index != -1)
                    {
                        Introduce(ref thisKeyIndex, links.ElementAt(index).First, 1, 0);
                        index = links.ElementAt(index).Second;
                    }

                    if (delete)
                        ZeroState(ref thatKeyIndex);
                }
            }
            else
                Logging.Error("Adding records to Immutable collection after compacting it");
        }
        
        public void SubtractStrictlyPriorDifferences(ref int keyIndex, int timeIndex)
        {
            // no strictly prior differences to subtract. 
        }

        public void ZeroState(ref int keyIndex)
        {
            if (keyIndex == 0)
                return;

            if (heads != null)
            {
                heads[keyIndex] = -1;
                keyIndex = 0;
            }
            else
                Logging.Error("Zeroing parts of Immutable collection after compacting it");

        }

        public bool IsZero(ref int keyIndex)
        {
            return keyIndex == 0;
        }

        public void EnumerateCollectionAt(int keyIndex, int timeIndex, NaiadList<Weighted<R>> toFill)
        {
            toFill.Clear();

            if (keyIndex != 0)
            {
                if (heads != null)
                {
                    int index = heads[keyIndex];
                    while (index != -1)
                    {
                        toFill.Add(links.ElementAt(index).First.ToWeighted(1));
                        index = links.ElementAt(index).Second;
                    }
                }
                else
                {
                    for (int i = offsets[keyIndex - 1]; i < offsets[keyIndex]; i++)
                        toFill.Add(data[i].ToWeighted(1));
                }
            }
        }

        public void EnumerateDifferenceAt(int keyIndex, int timeIndex, NaiadList<Weighted<R>> toFill)
        {
            toFill.Clear();

            if (keyIndex != 0)
            {
                if (heads != null)
                {
                    int index = heads[keyIndex];
                    while (index != -1)
                    {
                        toFill.Add(links.ElementAt(index).First.ToWeighted(1));
                        index = links.ElementAt(index).Second;
                    }
                }
                else
                {
                    for (int i = offsets[keyIndex - 1]; i < offsets[keyIndex]; i++)
                        toFill.Add(data[i].ToWeighted(1));
                }
            }
        }

        public void RemoveStateInTimes(ref int keyIndex, Func<int, bool> keepTime)
        {
            throw new NotImplementedException();
        }

        public long CountDifferenceAt(int keyIndex, int timeIndex)
        {
            if (keyIndex == 0 || timeIndex != 0)
            {
                return 0L;
            }
            else
            {
                long entries = 0;
                if (heads != null)
                {
                    int index = heads[keyIndex];
                    while (index != -1)
                    {
                        ++entries;
                        index = links.ElementAt(index).Second;
                    }
                }
                else
                {
                    entries = offsets[keyIndex] - offsets[keyIndex - 1];
                }

                return entries;
            }
        }

        public void EnumerateTimes(int keyIndex, NaiadList<int> timelist)
        {
            if (keyIndex != 0)
            {
                var present = false;
                for (int i = 0; i < timelist.Count; i++)
                    if (timelist.Array[i] == 0)
                        present = true;

                //timelist.Clear();
                if (!present)
                    timelist.Add(0);
            }
        }

        public void EnsureStateIsCurrentWRTAdvancedTimes(ref int state)
        {
        }

        public void MarkUsedTimes(int state, bool[] usedTimes)
        {
            usedTimes[0] = true;
        }

        public void TransferTimesToNewInternTable(int state, int[] updateTime)
        {
        }

        public void InstallNewUpdateFunction(Func<int, int, bool> newLessThan, Func<int, int> newUpdateTime)
        {
        }

        public void Release()
        {
        }

        public void Compact()
        {
            if (heads != null)
            {
                if (debug) Console.Error.WriteLine("Compacting ImmutableTrace");

                var counts = new int[heads.Count];
                for (int i = 0; i < heads.Count; i++)
                {
                    int index = heads[i];
                    while (index != -1)
                    {
                        counts[i]++;
                        index = links.ElementAt(index).Second;
                    }
                }

                offsets = new int[heads.Count];
                for (int i = 1; i < offsets.Length; i++)
                    offsets[i] = offsets[i - 1] + counts[i];

                var pos = 0;
                data = new R[offsets[offsets.Length - 1]];
                for (int i = 1; i < offsets.Length; i++)
                {
                    int index = heads[i];
                    while (index != -1)
                    {
                        data[pos++] = links.ElementAt(index).First;
                        index = links.ElementAt(index).Second;
                    }
                }

                if (debug) Console.Error.WriteLine("Trace compacted; {0} elements to {1} elements", links.Count, data.Length);
                Console.Error.WriteLine("Trace compacted; {0} elements to {1} elements", links.Count, data.Length);

                heads = null;
                links = null;
                counts = null;
            }
        }

        /* Checkpoint format:
         * BOOL isMutable
         * if (isMutable)
         *     INT                     headsLength
         *     INT*headsLength         heads
         *     INT                     linksLength
         *     PAIR<R,INT>*linksLength links
         * else
         *     INT               offsetsLength
         *     INT*offsetsLength offsets
         *     INT               dataLength
         *     R*dataLength      data
         */
        public void Checkpoint(NaiadWriter writer)
        {
            writer.Write(heads != null); // Is mutable flag
            if (heads != null)
            {
                // Still mutable
                writer.Write(heads.Count);
                foreach (int head in heads)
                    writer.Write(head);

                writer.Write(links.Count);

                for (int i = 0; i < links.Count; i++)
                    writer.Write(links.ElementAt(i));
            }
            else
            {
                // Immutable
                writer.Write(offsets.Length);
                for (int i = 0; i < offsets.Length; ++i)
                    writer.Write(offsets[i]);

                writer.Write(data.Length);
                for (int i = 0; i < data.Length; ++i)
                    writer.Write(data[i]);
            }
        }

        public void Restore(NaiadReader reader)
        {
            bool isMutable = reader.Read<bool>();
            if (isMutable)
            {
                int headsCount = reader.Read<int>();
                this.heads = new List<int>(headsCount);
                for (int i = 0; i < headsCount; ++i)
                    this.heads.Add(reader.Read<int>());

                int linksCount = reader.Read<int>();
                //this.links = new List<Naiad.Pair<R, int>>(linksCount);
                this.links = new SpinedList<Microsoft.Research.Naiad.Pair<R, int>>();
                for (int i = 0; i < linksCount; ++i)
                    this.links.Add(reader.Read<Microsoft.Research.Naiad.Pair<R, int>>());
            }
            else
            {
                int offsetsLength = reader.Read<int>();
                offsets = new int[offsetsLength];
                for (int i = 0; i < offsets.Length; ++i)
                    offsets[i] = reader.Read<int>();

                int dataLength = reader.Read<int>();
                data = new R[dataLength];
                for (int i = 0; i < data.Length; ++i)
                    data[i] = reader.Read<R>();
            }
        }

        public bool Stateful { get { return true; } }

        public CollectionTraceImmutable()
        {
            if (debug) Console.Error.WriteLine("Allocated ImmutableTrace");
            heads = new List<int>();
            links = new SpinedList<Microsoft.Research.Naiad.Pair<R, int>>();// new List<Naiad.Pair<R, int>>();
            heads.Add(-1);
        }
    }

    internal class CollectionTraceImmutableNoHeap<R> : CollectionTraceCheckpointable<R>
        where R : IEquatable<R>
    {
        public void Introduce(ref int keyIndex, R record, long weight, int timeIndex)
        {
            keyIndex += (int) weight;
        }

        public void IntroduceFrom(ref int destkeyIndex, ref int sourceKeyIndex, bool delete = true)
        {
            destkeyIndex += sourceKeyIndex;
            if (delete)
                sourceKeyIndex = 0;
        }

        public void SubtractStrictlyPriorDifferences(ref int keyIndex, int timeIndex)
        {
        }

        public void ZeroState(ref int keyIndex)
        {
            keyIndex = 0;
        }

        public bool IsZero(ref int keyIndex)
        {
            return keyIndex == 0;
        }

        public void EnumerateCollectionAt(int keyIndex, int timeIndex, NaiadList<Weighted<R>> toFill)
        {
            toFill.Clear();
            if (keyIndex != 0)
                toFill.Add(default(R).ToWeighted(keyIndex));
        }

        public void EnumerateDifferenceAt(int keyIndex, int timeIndex, NaiadList<Weighted<R>> toFill)
        {
            toFill.Clear();
            if (keyIndex != 0)
                toFill.Add(default(R).ToWeighted(keyIndex));
        }

        public void RemoveStateInTimes(ref int keyIndex, Func<int, bool> keepTime)
        {
            throw new NotImplementedException();
        }

        public long CountDifferenceAt(int keyIndex, int timeIndex)
        {
            if (keyIndex == 0 || timeIndex != 0)
            {
                return 0L;
            }
            else
            {
                return 1L;
            }
        }

        public void EnumerateTimes(int keyIndex, NaiadList<int> timelist)
        {
            if (keyIndex != 0)
            {
                var present = false;
                for (int i = 0; i < timelist.Count; i++)
                    if (timelist.Array[i] == 0)
                        present = true;

                if (!present)
                    timelist.Add(0);
            }
        }

        public void EnsureStateIsCurrentWRTAdvancedTimes(ref int keyIndex)
        {
        }

        public void MarkUsedTimes(int keyIndex, bool[] usedTimes)
        {
            usedTimes[0] = true;
        }

        public void TransferTimesToNewInternTable(int state, int[] updateTime)
        {
        }

        public void InstallNewUpdateFunction(Func<int, int, bool> newLessThan, Func<int, int> newUpdateTime)
        {
        }

        public void Release()
        {
        }

        public void Compact()
        {
        }

        public void Checkpoint(NaiadWriter writer)
        {
            throw new NotImplementedException();
        }

        public void Restore(NaiadReader reader)
        {
            throw new NotImplementedException();
        }

        public bool Stateful { get { return true; } }
    }

    internal static class CheckpointExtensionMethods
    {
        public static long CheckpointKey<TTime, TValue>(
            this CollectionTrace<TValue> trace, int keyIndex, ICheckpoint<TTime> checkpoint,
            TTime[] times, NaiadWriter writer)
            where TTime : Time<TTime>
            where TValue : IEquatable<TValue>
        {
            NaiadList<int> timeList = new NaiadList<int>(16);
            NaiadList<Weighted<TValue>> values = new NaiadList<Weighted<TValue>>(16);

            // the caller wrote out the number of times before calling this
            long entries = 0;

            trace.EnumerateTimes(keyIndex, timeList);
            for (int i = 0; i < timeList.Count; ++i)
            {
                if (checkpoint.ContainsTime(times[timeList.Array[i]]))
                {
                    writer.Write(times[timeList.Array[i]]);

                    trace.EnumerateDifferenceAt(keyIndex, timeList.Array[i], values);

                    entries += values.Count + 1;

                    writer.Write(values.Count);
                    for (int v = 0; v < values.Count; ++v)
                    {
                        writer.Write(values.Array[v]);
                    }
                    values.Clear();
                }
            }

            return entries;
        }

        public static Pair<long,long> CountEntries<TTime, TValue>(
            this CollectionTrace<TValue> trace, int keyIndex,
            ICheckpoint<TTime> checkpoint, TTime[] times,
            bool countInside, bool countOutside)
            where TTime : Time<TTime>
            where TValue : IEquatable<TValue>
        {
            NaiadList<int> timeList = new NaiadList<int>(16);

            long insideEntries = 0;
            long outsideEntries = 0;

            trace.EnumerateTimes(keyIndex, timeList);
            for (int i = 0; i < timeList.Count; ++i)
            {
                bool inside = checkpoint.ContainsTime(times[timeList.Array[i]]);
                if (inside && countInside)
                {
                    insideEntries += trace.CountDifferenceAt(keyIndex, timeList.Array[i]) + 1;
                }
                if (!inside && countOutside)
                {
                    outsideEntries += trace.CountDifferenceAt(keyIndex, timeList.Array[i]) + 1;
                }
            }

            return insideEntries.PairWith(outsideEntries);
        }

        public static long RestoreKey<TTime, TValue>(
            this CollectionTrace<TValue> trace, ref int keyIndex,
            LatticeInternTable<TTime> internTable, NaiadReader reader)
            where TTime : Time<TTime>
            where TValue : IEquatable<TValue>
        {
            int numberOfTimes = reader.Read<int>();

            long entries = numberOfTimes;

            for (int i = 0; i < numberOfTimes; ++i)
            {
                TTime time = reader.Read<TTime>();
                int timeIndex = internTable.Intern(time);

                int numberOfValues = reader.Read<int>();
                for (int v = 0; v < numberOfValues; ++v)
                {
                    Weighted<TValue> weightedValue = reader.Read<Weighted<TValue>>();
                    trace.Introduce(ref keyIndex, weightedValue.record, weightedValue.weight, timeIndex);
                }

                entries += numberOfValues;
            }

            return entries;
        }
    }
}
