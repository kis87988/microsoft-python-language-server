﻿// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Inspection;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
    internal class PythonInspectorService : IPythonInspector, IDisposable {
        private readonly IServiceContainer _services;

        private readonly object _lock = new object();
        private JsonRpc _rpc;

        public PythonInspectorService(IServiceContainer services) {
            _services = services;
        }

        private JsonRpc Rpc {
            get {
                lock (_lock) {
                    if (_rpc?.IsDisposed == false) {
                        return _rpc;
                    }

                    _rpc = CreateJsonRpc();
                    return _rpc;
                }
            }
        }

        private JsonRpc CreateJsonRpc() {
            if (!InstallPath.TryGetFile("inspector.py", out var scriptPath)) {
                return null; // Throw?
            }

            var procServices = _services.GetService<IProcessServices>();
            var interpreter = _services.GetService<IPythonInterpreter>();

            return PythonRpc.Create(procServices, interpreter.Configuration.InterpreterPath, scriptPath);
        }

        public Task<ModuleMemberNamesResponse> GetModuleMemberNamesAsync(string moduleName, CancellationToken cancellationToken = default) {
            return Rpc.InvokeWithCancellationAsync<ModuleMemberNamesResponse>("moduleMemberNames", new[] { moduleName }, cancellationToken);
        }

        public Task<string> GetModuleVersionAsync(string moduleName, CancellationToken cancellationToken = default) {
            return Rpc.InvokeWithCancellationAsync<string>("moduleVersion", new[] { moduleName }, cancellationToken);
        }

        public void Dispose() {
            _rpc?.Dispose();
        }

        private class PythonRpc : JsonRpc {
            private readonly IProcessServices _procServices;
            private IProcess _process;
            private bool _disposed;

            private PythonRpc(IProcessServices procServices, IProcess process) : base(process.StandardInput.BaseStream, process.StandardOutput.BaseStream) {
                _procServices = procServices;
                _process = process;
                Disconnected += PythonRpc_Disconnected;
                StartListening();
            }

            private void PythonRpc_Disconnected(object sender, JsonRpcDisconnectedEventArgs e) {
                Dispose();
            }

            public static PythonRpc Create(IProcessServices procServices, string exe, params string[] args) {
                var startInfo = new ProcessStartInfo(exe, args.AsQuotedArguments()) {
                    UseShellExecute = false,
                    ErrorDialog = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                };

                var process = procServices.Start(startInfo);
                return new PythonRpc(procServices, process);
            }

            protected override void Dispose(bool disposing) {
                if (_disposed) {
                    return;
                }

                _disposed = true;

                Disconnected -= PythonRpc_Disconnected;

                base.Dispose(disposing);
                if (_process?.HasExited == false) {
                    _procServices.Kill(_process);
                    _process = null;
                }
            }
        }
    }
}
