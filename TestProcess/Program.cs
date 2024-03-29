﻿//
// NativeLand  Copyright (C) 2023-2024  Aptivi
//
// This file is part of NativeLand
//
// NativeLand is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// NativeLand is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NativeLand;
using NativeLand.Tools;
using Serilog;
using Serilog.Extensions.Logging;

namespace TestProcess
{
    internal class Program
    {
        private static ILoggerFactory _factory;

        internal static int Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
            var factory = new LoggerFactory(new[] {new SerilogLoggerProvider() });

            _factory = factory;
            try
            {
                File.Delete("libTestLib.dylib");
                File.Delete("libTestLib.so");
                File.Delete("TestLib.dll");
            }
            catch (Exception e)
            {
                Log.ForContext<Program>().Warning(e, $"Failed to cleanup libraries before running a test: {e.Message}");
            }

            try
            {
                CanLoadLibraryFromCurrentDirAndCallFunction();
                CanLoadLibraryFromTempDirAndCallFunction();
                return 0;
            }
            catch (Exception e)
            {
                Log.ForContext<Program>().Error($"Test failed with exception: {e.GetType().Name}, {e.Message}");
                return 1;
            }
        }

        private static int CanLoadLibraryFromCurrentDirAndCallFunction()
        {
            var accessor = new ResourceAccessor(Assembly.GetExecutingAssembly());
            var libManager = new LibraryManager(
                _factory,
                new LibraryItem(Platform.MacOS, Architecture.X64,
                    new LibraryFile("libTestLib.dylib", accessor.Binary("libTestLib.dylib"))),
                new LibraryItem(Platform.Windows, Architecture.X64,
                    new LibraryFile("TestLib.dll", accessor.Binary("TestLib.dll"))),
                new LibraryItem(Platform.Linux, Architecture.X64,
                    new LibraryFile("libTestLib.so", accessor.Binary("libTestLib.so"))),
                new LibraryItem(Platform.Linux, Architecture.Arm64,
                    new LibraryFile("libTestLib.so", accessor.Binary("libTestLib_Arm64.so"))));

            libManager.LoadNativeLibrary();

            int result = hello();

            Log.ForContext<Program>().Information($"Function result is {result}");

            return result == 42 ? 0 : 1;
        }

        private static int CanLoadLibraryFromTempDirAndCallFunction()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var accessor = new ResourceAccessor(Assembly.GetExecutingAssembly());
            var libManager = new LibraryManager(
                tempDir,
                _factory,
                new LibraryItem(Platform.MacOS, Architecture.X64,
                    new LibraryFile("libTestLib.dylib", accessor.Binary("libTestLib.dylib"))),
                new LibraryItem(Platform.Windows, Architecture.X64,
                    new LibraryFile("TestLib.dll", accessor.Binary("TestLib.dll"))),
                new LibraryItem(Platform.Linux, Architecture.X64,
                    new LibraryFile("libTestLib.so", accessor.Binary("libTestLib.so"))),
                new LibraryItem(Platform.Linux, Architecture.Arm64,
                    new LibraryFile("libTestLib.so", accessor.Binary("libTestLib_Arm64.so"))))
            {
                LoadLibraryExplicit = true
            };

            var item = libManager.FindItem();
            libManager.LoadNativeLibrary();

            int result;
            try
            {
                result = hello();
            }
            catch (DllNotFoundException)
            {
                if (item.Platform == Platform.MacOS)
                {
                    Log.ForContext<Program>().Warning("Hit an expected exception on MacOS. Skipping test.");
                    return 0;
                }

                throw;
            }

            Log.ForContext<Program>().Information($"Function result is {result}");

            return result == 42 ? 0 : 1;
        }

        [DllImport("TestLib")]
        private static extern int hello();
    }
}
