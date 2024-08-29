using System;

namespace Neo.Compiler.CSharp.TestContracts;

public class Contract_LocalFunction : SmartContract.Framework.SmartContract
{
    public static int TestSimpleLocalFunction()
    {
        int LocalAdd(int a, int b)
        {
            return a + b;
        }

        return LocalAdd(5, 3);
    }

    public static int TestLocalFunctionWithCapture()
    {
        int multiplier = 2;

        int LocalMultiply(int x)
        {
            return x * multiplier;
        }

        return LocalMultiply(5);
    }

    public static int TestRecursiveLocalFunction(int n)
    {
        int Factorial(int x)
        {
            if (x <= 1) return 1;
            return x * Factorial(x - 1);
        }

        return Factorial(n);
    }

    public static string TestLocalFunctionWithMultipleParameters(string firstName, string lastName)
    {
        string FormatName(string first, string last)
        {
            string result = $"{first} {last}";
            return result;
        }

        return FormatName(firstName, lastName);
    }

    public static int TestNestedLocalFunctions()
    {
        int Outer(int x)
        {
            int Inner(int y)
            {
                return y * 2;
            }

            return Inner(x) + 5;
        }

        return Outer(3);
    }
}
