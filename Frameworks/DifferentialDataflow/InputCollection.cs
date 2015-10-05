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
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;

using Microsoft.Research.Naiad.DataStructures;
using Microsoft.Research.Naiad.Dataflow;
using Microsoft.Research.Naiad.Scheduling;
using Microsoft.Research.Naiad.Runtime.Controlling;
using Microsoft.Research.Naiad.Serialization;
using Microsoft.Research.Naiad.Input;
using Microsoft.Research.Naiad;

namespace Microsoft.Research.Naiad.Frameworks.DifferentialDataflow
{
    internal class RecordObserver<R> : IObserver<R> where R : IEquatable<R>
    {
        private bool completedCalled;
        private readonly IObserver<IEnumerable<Weighted<R>>> collection;
        private readonly List<Weighted<R>> list;

        public RecordObserver(IObserver<IEnumerable<Weighted<R>>> collection)
        {
            this.completedCalled = false;
            this.collection = collection;
            list = new List<Weighted<R>>();
        }

        public void OnNext(R record)
        {
            this.OnNext(record, 1);
        }

        public void OnNext(R record, int weight)
        {
            Debug.Assert(!this.completedCalled);
            list.Add(new Weighted<R>(record, weight));
        }

        public void OnCompleted()
        {
            Debug.Assert(!this.completedCalled);
            this.collection.OnNext(list);
        }

        public void OnError(Exception error)
        {
            throw error;
        }
    }

    /// <summary>
    /// Extension methods
    /// </summary>
    public static class InputCollectionExtensionMethods
    {
        /// <summary>
        /// Creates a new <see cref="InputCollection{TRecord}"/> in the given computation.
        /// </summary>
        /// <typeparam name="TRecord">The type of records in the collection.</typeparam>
        /// <param name="computation">The graph manager for the computation.</param>
        /// <returns>The new <see cref="InputCollection{TRecord}"/>.</returns>
        public static InputCollection<TRecord> NewInputCollection<TRecord>(this Computation computation)
            where TRecord : IEquatable<TRecord>
        {
            return new IncrementalCollection<TRecord>(computation);
        }

        /// <summary>
        /// Creates a new <see cref="SubBatchInputCollection{TRecord,TTime}"/> in the given computation.
        /// </summary>
        /// <typeparam name="TRecord">The type of records in the collection.</typeparam>
        /// <typeparam name="TTime">The time type in the outer batch.</typeparam>
        /// <param name="computation">The graph manager for the computation.</param>
        /// <returns>The new <see cref="SubBatchInputCollection{TRecord,TTime}"/>.</returns>
        public static SubBatchInputCollection<TRecord, TTime> NewInputCollection<TRecord, TTime>(this Computation computation)
            where TRecord : IEquatable<TRecord>
            where TTime : Time<TTime>
        {
            return new SubBatchIncrementalCollection<TRecord,TTime>(computation);
        }

        /// <summary>
        /// Create a new batched entry subgraph
        /// </summary>
        /// <typeparam name="S">type of records exiting the subgraph</typeparam>
        /// <typeparam name="T">time type of records after exiting</typeparam>
        /// <param name="computation">computation graph</param>
        /// <param name="entryComputation">function within the subgraph</param>
        /// <returns>the function after exiting the subbatches</returns>
        public static Collection<S, T> BatchedEntry<S, T>(this Computation computation, Func<Dataflow.BatchEntry.BatchContext<T>, Collection<S, BatchIn<T>>> entryComputation)
            where S : IEquatable<S>
            where T : Time<T>
        {
            var helper = new Dataflow.BatchEntry.BatchContext<T>();

            var batchedOutput = entryComputation(helper);

            return helper.ExitBatch(batchedOutput.Output).ToCollection();
        }
    }

    internal class IncrementalCollection<R> : TypedCollection<R, Epoch>, InputCollection<R>
        where R : IEquatable<R>
    {
        private readonly Microsoft.Research.Naiad.Input.BatchedDataSource<Weighted<R>> inputVertex;
        private readonly Stream<Weighted<R>, Epoch> stream;

        public override Stream<Weighted<R>, Epoch> Output
        {
            get { return this.stream; }
        }

        public void OnCompleted()
        {
            inputVertex.OnCompleted();
        }

        public void OnCompleted(IEnumerable<Weighted<R>> value)
        {
            inputVertex.OnCompleted(value);
        }

        public void OnError(Exception error)
        {
            inputVertex.OnError(error);
        }

        public void OnNext(IEnumerable<Weighted<R>> value)
        {
            inputVertex.OnNext(value);
        }

        internal override StreamContext Context { get { return this.stream.Context; } }

        public IncrementalCollection(Microsoft.Research.Naiad.Computation manager)
        {
            this.inputVertex = new BatchedDataSource<Weighted<R>>();
            this.stream = manager.NewInput(inputVertex);
        }
    }

    internal class SubBatchIncrementalCollection<R, T> : TypedCollection<R, BatchIn<T>>, SubBatchInputCollection<R, T>
        where R : IEquatable<R>
        where T : Time<T>
    {
        private readonly Microsoft.Research.Naiad.Input.SubBatchDataSource<Weighted<R>, T> inputVertex;
        private readonly Stream<Weighted<R>, BatchIn<T>> stream;

        public override Stream<Weighted<R>, BatchIn<T>> Output
        {
            get { return this.stream; }
        }

        public void OnCompleted()
        {
            inputVertex.OnCompleted();
        }

        public void OnCompleted(IEnumerable<Weighted<R>> value)
        {
            inputVertex.OnCompleted(value);
        }

        public void OnError(Exception error)
        {
            inputVertex.OnError(error);
        }

        public void OnNext(IEnumerable<Weighted<R>> value)
        {
            inputVertex.OnNext(value);
        }

        public void CompleteOuterBatch()
        {
            inputVertex.CompleteOuterBatch();
        }

        public void SetOuterBatch(T time)
        {
            inputVertex.SetOuterBatch(time);
        }

        internal override StreamContext Context { get { return this.stream.Context; } }

        public SubBatchIncrementalCollection(Microsoft.Research.Naiad.Computation manager)
        {
            this.inputVertex = new SubBatchDataSource<Weighted<R>, T>();
            this.stream = manager.NewInput(inputVertex);
        }
    }
}
