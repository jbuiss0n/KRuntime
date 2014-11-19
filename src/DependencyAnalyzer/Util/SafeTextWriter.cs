// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace DependencyAnalyzer.Util
{
    public class SafeTextWriter : IDisposable
    {
        private readonly TextWriter _output;
        private readonly bool       _toDispose;

        public static SafeTextWriter CreateOutput(string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new SafeTextWriter(Console.Out, toDispose: false);
            }
            else
            {
                return new SafeTextWriter(new StreamWriter(path), toDispose: true);
            }
        }

        private SafeTextWriter(TextWriter output, bool toDispose = true)
        {
            _output = output;
            _toDispose = toDispose;
        }

        public TextWriter Writer
        {
            get { return _output; }
        }

        public void Dispose()
        {
            _output.Flush();

            if (_toDispose)
            {
                _output.Dispose();
            }
        }
    }
}