// 
//  Author:
//    Marek Habersack <grendel@twistedcode.net>
// 
//  Copyright (c) 2010, Marek Habersack
// 
//  All rights reserved.
// 
//  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
//       the documentation and/or other materials provided with the distribution.
//     * Neither the name of Marek Habersack nor the names of the contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
//  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//  LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//  NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace MonoDepGen
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			if (args.Length < 2) {
				Console.WriteLine ("Usage: monodepgen PATH_TO_LIB_DIR OUTPUT_PREFIX");
				Environment.Exit (1);
			}
			
			string prefix = args [1];
			using (FileStream bigPicture = File.Open (prefix + "_big_picture.dot", FileMode.Create, FileAccess.Write, FileShare.None)) {
				using (StreamWriter bpsw = new StreamWriter (bigPicture, Encoding.UTF8)) {
					SortedDictionary <string, List <string>> deps = GatherDeps (args [0]);
					GenerateFiles (bpsw, prefix, deps);
				}
			}
		}
		
		static void GenerateFiles (StreamWriter bigPictureWriter, string prefix, SortedDictionary <string, List <string>> deps)
		{
			bigPictureWriter.WriteLine ("digraph Deps {");
			var processed = new Dictionary <string, bool> ();
			foreach (string key in deps.Keys) {
				bigPictureWriter.Write ("\t\"{0}\" -> {{ ", key);
				using (FileStream dll = File.Open (prefix + key + ".dot", FileMode.Create, FileAccess.Write, FileShare.None)) {
					using (StreamWriter sw = new StreamWriter (dll)) {
						foreach (string dep in deps [key]) {
							if (dep == "mscorlib.dll" || dep == "System.dll")
								continue;
							bigPictureWriter.Write ("\"" + dep + "\" ");
							if (processed.ContainsKey (key))
								continue;
							processed.Add (key, true);
							GenerateDllFile (sw, key, deps);
						}
						bigPictureWriter.WriteLine ("};");
					}
				}
			}
			bigPictureWriter.WriteLine ("}");
		}
		
		static void GenerateDllFile (StreamWriter sw, string dll, SortedDictionary <string, List <string>> deps)
		{
			sw.WriteLine ("digraph \"{0}\" {{", dll);
			var processed = new Dictionary <string, bool> ();
			GenerateDepsForFile (sw, dll, deps, processed);
			sw.WriteLine ("}");
		}
		
		static void GenerateDepsForFile (StreamWriter sw, string dll, SortedDictionary <string, List <string>> deps, Dictionary <string, bool> processed)
		{
			if (!deps.ContainsKey (dll))
				return;
			
			string directdep;
			foreach (string dep in deps [dll]) {
				if (dep == "mscorlib.dll" || dep == "System.dll")
					continue;
				directdep = dll + "->" + dep;
				if (processed.ContainsKey (directdep))
					continue;
				processed.Add (directdep, true);
				sw.WriteLine ("\t\"{0}\" -> \"{1}\";", dll, dep);
				if (processed.ContainsKey (dep))
					continue;
				processed.Add (dep, true);
				GenerateDepsForFile (sw, dep, deps, processed);
			}
		}
		
		static SortedDictionary <string, List <string>> GatherDeps (string libdir)
		{
			var ret = new SortedDictionary <string, List <string>> (StringComparer.OrdinalIgnoreCase);
			
			string fname;
			Assembly asm;
			foreach (var path in Directory.GetFiles (libdir, "*.dll")) {
				fname = Path.GetFileName (path);
				if (fname == "mscorlib")
					continue;
				
				try {
					asm = Assembly.ReflectionOnlyLoadFrom (path);
					ret.Add (fname, LoadDependencies (asm));
				} catch (Exception ex) {
					Console.WriteLine ("Failed to load assembly '{0}'. {1}", fname, ex.Message);
				}
			}
			
			return ret;
		}
		
		static List <string> LoadDependencies (Assembly asm)
		{
			var ret = new List <string> ();
			
			AssemblyName[] assemblies = asm.GetReferencedAssemblies ();
			foreach (AssemblyName aname in assemblies)
				ret.Add (aname.Name + ".dll");
			
			ret.Sort ();
			return ret;
		}
	}
}
