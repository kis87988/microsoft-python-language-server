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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Schema;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
        private class Node {
            public readonly ImmutableArray<Node> Children;
            public readonly string Name;
            public readonly string ModulePath;
            public readonly string FullModuleName;
            public bool IsModule => ModulePath != null;

            private Node(string name, ImmutableArray<Node> children, string modulePath, string fullModuleName) {
                Name = name;
                Children = children;
                ModulePath = modulePath;
                FullModuleName = fullModuleName;
            }

            public static Node CreateDefaultRoot()
                => new Node("*", ImmutableArray<Node>.Empty, null, null);

            public static Node CreateBuiltinRoot()
                => new Node(":", ImmutableArray<Node>.Empty, null, null);

            public static Node CreateBuiltinRoot(ImmutableArray<Node> builtins)
                => new Node(":", builtins, null, null);

            public static Node CreateRoot(string path) 
                => new Node(PathUtils.NormalizePath(path), ImmutableArray<Node>.Empty, null, null);

            public static Node CreateModule(string name, string modulePath, string fullModuleName)
                => new Node(name, ImmutableArray<Node>.Empty, modulePath, fullModuleName);

            public static Node CreatePackage(string name, Node child)
                => new Node(name, ImmutableArray<Node>.Empty.Add(child), null, null);

            public static Node CreateBuiltinModule(string name)
                => new Node(name, ImmutableArray<Node>.Empty, null, name);
            
            public Node ToModuleNode(string modulePath, string fullModuleName) => new Node(Name, Children, modulePath, fullModuleName);
            public Node ToPackage() => new Node(Name, Children, null, null);
            public Node AddChild(Node child) => new Node(Name, Children.Add(child), ModulePath, FullModuleName);
            public Node ReplaceChildAt(Node child, int index) => new Node(Name, Children.ReplaceAt(index, child), ModulePath, FullModuleName);
            public Node RemoveChildAt(int index) => new Node(Name, Children.RemoveAt(index), ModulePath, FullModuleName);

            public bool TryGetChild(string childName, out Node child) {
                var index = GetChildIndex(childName);
                if (index == -1) {
                    child = default;
                    return false;
                }

                child = Children[index];
                return true;
            }

            public int GetChildIndex(string childName) => Children.IndexOf(childName, NameEquals);
            public int GetChildIndex(string modulePath, (int start, int length) nameSpan) 
                => Children.IndexOf((modulePath, nameSpan.start, nameSpan.length), NameEquals);

            private string DebuggerDisplay {
                get {
                    var sb = new StringBuilder();
                    GetDebuggerDisplay(sb, 0);
                    return sb.ToString();
                }
            }

            private void GetDebuggerDisplay(StringBuilder sb, int offset) {
                sb.Append(' ', offset).Append(IsModule ? Name : "[" + Name + "]").AppendLine();
                foreach (var child in Children) {
                    child.GetDebuggerDisplay(sb, offset + 2);
                }
            }

            private static bool NameEquals(Node n, string name) => n.Name.EqualsOrdinal(name);

            private static bool NameEquals(Node n, (string str, int start, int length) span) 
                    => n.Name.Length == span.length && n.Name.EqualsOrdinal(0, span.str, span.start, span.length);
        }
    }
}
