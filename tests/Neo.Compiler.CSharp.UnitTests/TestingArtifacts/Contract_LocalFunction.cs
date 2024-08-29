using Neo.Cryptography.ECC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract.Testing;

public abstract class Contract_LocalFunction(Neo.SmartContract.Testing.SmartContractInitialize initialize) : Neo.SmartContract.Testing.SmartContract(initialize), IContractInfo
{
    #region Compiled data

    public static Neo.SmartContract.Manifest.ContractManifest Manifest => Neo.SmartContract.Manifest.ContractManifest.Parse(@"{""name"":""Contract_LocalFunction"",""groups"":[],""features"":{},""supportedstandards"":[],""abi"":{""methods"":[{""name"":""testSimpleLocalFunction"",""parameters"":[],""returntype"":""Integer"",""offset"":0,""safe"":false},{""name"":""testLocalFunctionWithCapture"",""parameters"":[],""returntype"":""Integer"",""offset"":9,""safe"":false},{""name"":""testRecursiveLocalFunction"",""parameters"":[{""name"":""n"",""type"":""Integer""}],""returntype"":""Integer"",""offset"":22,""safe"":false},{""name"":""testLocalFunctionWithMultipleParameters"",""parameters"":[{""name"":""firstName"",""type"":""String""},{""name"":""lastName"",""type"":""String""}],""returntype"":""String"",""offset"":33,""safe"":false},{""name"":""testNestedLocalFunctions"",""parameters"":[],""returntype"":""Integer"",""offset"":45,""safe"":false}],""events"":[]},""permissions"":[],""trusts"":[],""extra"":{""nef"":{""optimization"":""All""}}}");

    /// <summary>
    /// Optimization: "All"
    /// </summary>
    public static Neo.SmartContract.NefFile Nef => Neo.IO.Helper.AsSerializable<Neo.SmartContract.NefFile>(Convert.FromBase64String(@"TkVGM1Rlc3RpbmdFbmdpbmUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADUTFTQDQFcAA0BXAQAScBU0A0BXAAJAVwABeDQDQFcAAkBXAAJ5eDQDQFcAA0ATNANAVwACQEjckSY="));

    #endregion

    #region Unsafe methods

    /// <summary>
    /// Unsafe method
    /// </summary>
    [DisplayName("testLocalFunctionWithCapture")]
    public abstract BigInteger? TestLocalFunctionWithCapture();

    /// <summary>
    /// Unsafe method
    /// </summary>
    [DisplayName("testLocalFunctionWithMultipleParameters")]
    public abstract string? TestLocalFunctionWithMultipleParameters(string? firstName, string? lastName);

    /// <summary>
    /// Unsafe method
    /// </summary>
    [DisplayName("testNestedLocalFunctions")]
    public abstract BigInteger? TestNestedLocalFunctions();

    /// <summary>
    /// Unsafe method
    /// </summary>
    [DisplayName("testRecursiveLocalFunction")]
    public abstract BigInteger? TestRecursiveLocalFunction(BigInteger? n);

    /// <summary>
    /// Unsafe method
    /// </summary>
    [DisplayName("testSimpleLocalFunction")]
    public abstract BigInteger? TestSimpleLocalFunction();

    #endregion

}
