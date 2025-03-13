// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore.Generator
{
    /// <summary>
    /// Generates proxy classes for scene to scene actions decorated with <see cref="S2SApiAttribute"/> in Stormancer.
    /// </summary>
    /// <remarks>
    /// Controllers must be decorated with <see cref="ServiceAttribute"/> to specify service locator informations.
    /// If the client code needs to send or receive raw data using the pipe interface, the action shall have a, IS2SRequestContext parameter
    /// decorated with <see cref="S2SContextUsageAttribute"/>. If at least one such parameter exist, the generator will expose the underlaying pipes.
    /// </remarks>
    [Generator]
    public class DbContextSourceGenerator : ISourceGenerator
    {
        /*lang=c#-test*/
        private string _source = @"using Microsoft.EntityFrameworkCore.Design;
using Stormancer.Server.Hosting;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace {{namespace}}
{
	public class DbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext(string[] args)
		{
			var host = ServerApplication.CreateDesignTimeHost(builder => builder.AddAllStartupActions());
			var scope = host.DependencyResolver.CreateChild(Stormancer.Server.Plugins.API.Constants.ApiRequestTag);

			// We can use .Result because in design time mode, every tasks are run synchronously.
			return scope.Resolve<DbContextAccessor>().GetDbContextAsync().Result;
		}
	}
}
";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void Execute(GeneratorExecutionContext context)
        {
            
            if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace))
            {
                return;
            }
         
            var content = _source.Replace("{{namespace}}",rootNamespace);
            context.AddSource("DbContextFactory.cs", SourceText.From(content.ToString(), Encoding.UTF8));
           

        }






        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(GeneratorInitializationContext context)
        {
            //context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        //class SyntaxReceiver : ISyntaxReceiver
        //{
        //    public List<ClassDeclarationSyntax> ClassCandidates { get; } = new List<ClassDeclarationSyntax>();
        //    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        //    {
        //        if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
        //        {
        //            ClassCandidates.Add(classDeclarationSyntax);
        //        }
        //    }
        //}

    }

}
