using audit.CodeAnalysis;
using Xunit;

namespace specs
{
    public class TSComplexityCalculatorTests
    {
        [Fact]
        public void CalculateClassComplexity_NullInput_ReturnsZero()
        {
            Assert.Equal(0, TSComplexityCalculator.CalculateClassComplexity(null));
        }

        [Fact]
        public void CalculateClassComplexity_EmptyInput_ReturnsZero()
        {
            Assert.Equal(0, TSComplexityCalculator.CalculateClassComplexity([]));
        }

        [Fact]
        public void CalculateClassComplexity_MethodWithNoBranches_ReturnsBaseComplexityOfOne()
        {
            string[] lines =
            [
                "public main(): void {",
                "}"
            ];

            Assert.Equal(1, TSComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_SingleIf_AddsOneDecisionPoint()
        {
            string[] lines =
            [
                "public main(): void {",
                "  if (x) { }",
                "}"
            ];

            Assert.Equal(2, TSComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_LogicalOperators_EachAddOne()
        {
            string[] lines =
            [
                "public main(): void {",
                "  if (a && b || c) { }",
                "}"
            ];

            // base(1) + if(1) + &&(1) + ||(1)
            Assert.Equal(4, TSComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_KeywordsInCommentsAndStrings_AreIgnored()
        {
            string[] lines =
            [
                "public main(): void {",
                "  // if (x) for while switch",
                "  const s = \"if for while && ||\";",
                "}"
            ];

            Assert.Equal(1, TSComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_LoopsSwitchTernaryAndArrow_AreCounted()
        {
            string[] lines =
            [
                "public main(): void {",
                "  for (let i = 0; i < n; i++) { }",
                "  while (x) { }",
                "  switch (y) { case 1: break; }",
                "  const f = () => 1;",
                "  const g = a ? b : c;",
                "}"
            ];

            // base(1) + for(1) + while(1) + switch(1) + case(1) + arrow(1) + ternary(1)
            Assert.Equal(7, TSComplexityCalculator.CalculateClassComplexity(lines));
        }

        [Fact]
        public void CalculateClassComplexity_MultipleMethods_SumsComplexity()
        {
            string[] lines =
            [
                "public a(): void {",
                "  if (x) { }",
                "}",
                "public b(): void {",
                "  if (y) { }",
                "}"
            ];

            // two methods, each base(1) + if(1)
            Assert.Equal(4, TSComplexityCalculator.CalculateClassComplexity(lines));
        }
    }
}
