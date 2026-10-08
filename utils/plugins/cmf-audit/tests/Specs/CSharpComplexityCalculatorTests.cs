using audit.CodeAnalysis;
using Xunit;

namespace specs
{
    public class CSharpComplexityCalculatorTests
    {
        [Fact]
        public void CalculateClassComplexity_NoMethods_ReturnsZero()
        {
            string[] lines =
            [
                "public class Foo",
                "{",
                "    private int _x;",
                "}"
            ];

            Assert.Equal(0, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_MethodWithNoBranches_ReturnsBaseComplexityOfOne()
        {
            string[] lines =
            [
                "public void Foo()",
                "{",
                "}"
            ];

            Assert.Equal(1, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_SingleIf_AddsOneDecisionPoint()
        {
            string[] lines =
            [
                "public void Foo()",
                "{",
                "    if (x) { }",
                "}"
            ];

            Assert.Equal(2, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_IfElseIf_CountsBoth()
        {
            string[] lines =
            [
                "public void Foo()",
                "{",
                "    if (a) { }",
                "    else if (b) { }",
                "}"
            ];

            // base(1) + if(1) + else-if(1)
            Assert.Equal(3, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_ForeachAndWhile_EachAddOne()
        {
            string[] lines =
            [
                "public void Foo()",
                "{",
                "    foreach (var x in y) { }",
                "    while (z) { }",
                "}"
            ];

            // base(1) + foreach(1) + while(1)
            Assert.Equal(3, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_Catch_AddsOne()
        {
            string[] lines =
            [
                "public void Foo()",
                "{",
                "    try { } catch (Exception e) { }",
                "}"
            ];

            Assert.Equal(2, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_LinqWhereAndSelect_AreCountedAsDecisionPoints()
        {
            string[] lines =
            [
                "public void Foo()",
                "{",
                "    list.Where(i => i > 0).Select(i => i);",
                "}"
            ];

            // base(1) + Where(1) + Select(1)
            Assert.Equal(3, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_MultipleMethods_SumsComplexity()
        {
            string[] lines =
            [
                "public void A()",
                "{",
                "    if (x) { }",
                "}",
                "private static int B()",
                "{",
                "    if (y) { }",
                "}"
            ];

            // two methods, each base(1) + if(1)
            Assert.Equal(4, CSharpComplexityCalculator.CalculateClassComplexity(lines));
        }
    }
}
