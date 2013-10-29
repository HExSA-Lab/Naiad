/*
 * Naiad ver. 0.2
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

﻿using Naiad.Dataflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Naiad.Frameworks.DifferentialDataflow.Operators
{
    class Intersect<S, T> : OperatorImplementations.BinaryStatefulOperator<S, T>
        where S : IEquatable<S>
        where T : Naiad.Time<T>
    {
        protected override Int64 WeightFunction(Int64 weight1, Int64 weight2)
        {
            return Math.Min(weight1, weight2);
        }

        public Intersect(int index, Stage<T> collection, bool input1Immutable, bool input2Immutable)
            : base(index, collection, input1Immutable, input2Immutable)
        {
        }
    }
}
