using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract.Testing;
using Neo.SmartContract.Testing.Exceptions;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Numerics;

namespace Neo.Compiler.CSharp.UnitTests
{
    [TestClass]
    public class UnitTest_LocalFunction : DebugAndTestBase<Contract_LocalFunction>
    {
        [TestMethod]
        public void Test_SimpleLocalFunction()
        {

            var result = Contract.TestSimpleLocalFunction();
            Assert.AreEqual(8, result);
            AssertGasConsumed(1065300);
        }
        //
        // [TestMethod]
        // public void Test_LocalFunctionWithCapture()
        // {
        //     var result = Contract.TestLocalFunctionWithCapture();
        //     Assert.AreEqual(10, result);
        //     AssertGasConsumed(1084680);
        // }
        //
        // [TestMethod]
        // public void Test_RecursiveLocalFunction()
        // {
        //     var result = Contract.TestRecursiveLocalFunction(5);
        //     Assert.AreEqual(120, result);
        //     AssertGasConsumed(1216080);
        // }
        //
        // [TestMethod]
        // public void Test_LocalFunctionWithMultipleParameters()
        // {
        //     var result = Contract.TestLocalFunctionWithMultipleParameters("John", "Doe");
        //     Assert.AreEqual("John Doe", result);
        //     AssertGasConsumed(1387590);
        // }
        //
        // [TestMethod]
        // public void Test_NestedLocalFunctions()
        // {
        //     var result = Contract.TestNestedLocalFunctions();
        //     Assert.AreEqual(11, result);
        //     AssertGasConsumed(1084350);
        // }
        //
        // [TestMethod]
        // public void Test_LocalFunctionAsDelegate()
        // {
        //     var result = Contract.TestLocalFunctionAsDelegate();
        //     Assert.AreEqual(10, result);
        //     AssertGasConsumed(1066410);
        // }
    }
}
