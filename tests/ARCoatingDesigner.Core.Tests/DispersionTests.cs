using ARCoatingDesigner.Core.Dispersion;
using ARCoatingDesigner.Core.Models;
using Xunit;

namespace ARCoatingDesigner.Core.Tests
{
    public class DispersionTests
    {
        [Fact]
        public void MgF2_Index_At550nm_ShouldBeAbout1_38()
        {
            var material = CoatingMaterial.MgF2;
            var (n, k) = material.GetNK(0.55);
            Assert.InRange(n, 1.37, 1.39);
            Assert.Equal(0, k);
        }

        [Fact]
        public void SiO2_Index_At550nm_ShouldBeAbout1_46()
        {
            var material = CoatingMaterial.SiO2;
            var (n, k) = material.GetNK(0.55);
            Assert.InRange(n, 1.45, 1.47);
            Assert.Equal(0, k);
        }

        [Fact]
        public void TiO2_Index_At550nm_ShouldBeAbout2_35()
        {
            var material = CoatingMaterial.TiO2;
            var (n, k) = material.GetNK(0.55);
            Assert.InRange(n, 2.25, 2.45);
            Assert.Equal(0, k);
        }

        [Fact]
        public void Al2O3_Index_At550nm_ShouldBeAbout1_77()
        {
            var material = CoatingMaterial.Al2O3;
            var (n, k) = material.GetNK(0.55);
            Assert.InRange(n, 1.75, 1.80);
            Assert.Equal(0, k);
        }

        [Fact]
        public void Sellmeier_Dispersion_HigherAtShorterWavelengths()
        {
            var material = CoatingMaterial.MgF2;
            var (n400, _) = material.GetNK(0.40);
            var (n550, _) = material.GetNK(0.55);
            var (n700, _) = material.GetNK(0.70);

            Assert.True(n400 > n550);
            Assert.True(n550 > n700);
        }

        [Fact]
        public void Cauchy_Dispersion_HigherAtShorterWavelengths()
        {
            var material = CoatingMaterial.TiO2;
            var (n400, _) = material.GetNK(0.40);
            var (n700, _) = material.GetNK(0.70);

            Assert.True(n400 > n700);
        }

        [Fact]
        public void Constant_Material_SameAtAllWavelengths()
        {
            var material = CoatingMaterial.Custom("Test", 1.75);
            var (n1, _) = material.GetNK(0.40);
            var (n2, _) = material.GetNK(0.70);

            Assert.Equal(1.75, n1);
            Assert.Equal(1.75, n2);
        }

        [Fact]
        public void CauchyCoefficients_CalcN_ReturnsExpectedValue()
        {
            var cauchy = new CauchyCoefficients { A = 2.20, B = 0.030, C = 0.003 };
            double n = cauchy.CalcN(0.55);
            // A + B/λ² + C/λ⁴ = 2.20 + 0.030/0.3025 + 0.003/0.09150625
            double expected = 2.20 + 0.030 / (0.55 * 0.55) + 0.003 / (0.55 * 0.55 * 0.55 * 0.55);
            Assert.Equal(expected, n, 10);
        }

        [Fact]
        public void SellmeierCoefficients_Standard_HasA1()
        {
            var s = SellmeierCoefficients.Standard(0.5, 0.01);
            Assert.Equal(1.0, s.A);
        }

        [Fact]
        public void SellmeierCoefficients_Modified_HasCustomA()
        {
            var s = SellmeierCoefficients.Modified(2.5, 0.5, 0.01);
            Assert.Equal(2.5, s.A);
        }
    }
}
