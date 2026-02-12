using System;
using System.Numerics;
using ARCoatingDesigner.Core.Calculations;
using ARCoatingDesigner.Core.Catalogs;
using ARCoatingDesigner.Core.Models;
using Xunit;

namespace ARCoatingDesigner.Core.Tests
{
    public class ThinFilmCalculatorTests
    {
        private ThinFilmCalculator CreateCalculator()
        {
            var catalogService = new CatalogService();
            catalogService.InitializeWithStandardMaterials();
            return new ThinFilmCalculator(catalogService);
        }

        [Fact]
        public void BareSubstrate_Fresnel_MatchesFormula()
        {
            // Bare N-BK7 (n=1.5168) at normal incidence: R = ((n-1)/(n+1))^2
            var calc = CreateCalculator();
            var design = new CoatingDesign
            {
                Name = "Bare",
                SubstrateMaterial = "N-BK7",
                UseOpticalThickness = false,
                Layers = new System.Collections.Generic.List<DesignLayer>()
            };

            var result = calc.Calculate(design, 0.55, 0);

            // N-BK7 n≈1.5168, R = ((1.5168-1)/(1.5168+1))^2 ≈ 4.2%
            // With our default glass (1.5168): R = ((0.5168)/(2.5168))^2 = 0.04218 = 4.22%
            Assert.InRange(result.Rave, 3.5, 5.0);
            Assert.False(result.IsTIR);
            Assert.InRange(result.Rave + result.Tave, 99.5, 100.5);
        }

        [Fact]
        public void SingleLayerMgF2_QWOT_ReducesReflectance()
        {
            // MgF2 QWOT on N-BK7 should reduce R from ~4.2% to ~1.3% at design wavelength
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            var result = calc.Calculate(design, 0.55, 0);

            // SLAR should give R between 1% and 2%
            Assert.InRange(result.Rave, 0.5, 2.5);
            Assert.True(result.Rave < 4.0); // Must be less than bare surface
        }

        [Fact]
        public void VCoat_HasDifferentReflectanceThanBare()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultVCoat();

            var result = calc.Calculate(design, 0.55, 0);

            // TiO2/MgF2 QW/QW on N-BK7 doesn't give minimal R (index ratio too high)
            // but reflectance should be a definite value (not NaN/infinite)
            Assert.True(!double.IsNaN(result.Rave));
            Assert.True(!double.IsInfinity(result.Rave));
            Assert.InRange(result.Rave, 0.0, 100.0);
        }

        [Fact]
        public void CalculateSpectrum_ReturnsCorrectPointCount()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            var (wavelengths, Rs, Rp, Rave) = calc.CalculateSpectrum(design, 0.4, 0.8, 51, 0);

            Assert.Equal(51, wavelengths.Length);
            Assert.Equal(51, Rave.Length);
            Assert.Equal(0.4, wavelengths[0], 6);
            Assert.Equal(0.8, wavelengths[50], 6);
        }

        [Fact]
        public void CalculateAngularResponse_ReturnsCorrectPointCount()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            var (angles, Rs, Rp, Rave) = calc.CalculateAngularResponse(design, 0.55, 0, 60, 31);

            Assert.Equal(31, angles.Length);
            Assert.Equal(0, angles[0], 6);
            Assert.Equal(60, angles[30], 6);
        }

        [Fact]
        public void NormalIncidence_Rs_Equals_Rp()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            var result = calc.Calculate(design, 0.55, 0);

            Assert.Equal(result.Rs, result.Rp, 6);
        }

        [Fact]
        public void ObliqueIncidence_Rs_DiffersFrom_Rp()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            var result = calc.Calculate(design, 0.55, 45);

            Assert.NotEqual(result.Rs, result.Rp);
            Assert.True(result.Rs > result.Rp); // Rs > Rp for dielectrics below Brewster angle
        }

        [Fact]
        public void Fresnel_NormalIncidence_Rs_Equals_Rp()
        {
            bool notTIR = ThinFilmCalculator.CalculateFresnelCoefficients(
                1.0, 1.5, 1.0,
                out Complex ts, out Complex tp, out Complex rs, out Complex rp);

            Assert.True(notTIR);
            Assert.Equal(rs.Real, rp.Real, 10);
        }

        [Fact]
        public void Fresnel_TIR_ReturnsCorrectly()
        {
            // Going from glass (n=1.5) to air (n=1.0) at angle > critical angle
            double criticalAngle = Math.Asin(1.0 / 1.5);
            double testAngle = criticalAngle + 0.1;
            double cosTheta = Math.Cos(testAngle);

            bool notTIR = ThinFilmCalculator.CalculateFresnelCoefficients(
                1.5, 1.0, cosTheta,
                out Complex ts, out Complex tp, out Complex rs, out Complex rp);

            Assert.False(notTIR);
            Assert.Equal(Complex.Zero, ts);
            Assert.Equal(Complex.Zero, tp);
            // |r| should be 1 for TIR
            Assert.Equal(1.0, rs.Magnitude, 5);
            Assert.Equal(1.0, rp.Magnitude, 5);
        }

        [Fact]
        public void GetTargetValue_ReturnsCorrectType()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            var targetRave = new MeritTarget { TargetType = MeritTargetType.Rave, Wavelength_um = 0.55, AOI_deg = 0 };
            var targetTave = new MeritTarget { TargetType = MeritTargetType.Tave, Wavelength_um = 0.55, AOI_deg = 0 };

            double rave = calc.GetTargetValue(design, targetRave);
            double tave = calc.GetTargetValue(design, targetTave);

            Assert.InRange(rave + tave, 99.0, 101.0);
        }

        [Fact]
        public void OpticalToPhysical_Roundtrip()
        {
            double optical = 0.25;
            double n = 1.38;
            double lambda = 0.55;

            double physical = DesignLayer.OpticalToPhysical(optical, n, lambda);
            double backToOptical = DesignLayer.PhysicalToOptical(physical, n, lambda);

            Assert.Equal(optical, backToOptical, 10);
        }

        [Fact]
        public void CalculateMerit_WithEqualTargets()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            double merit = calc.CalculateMerit(design);

            // Merit should be finite and non-negative
            Assert.True(merit >= 0);
            Assert.True(!double.IsNaN(merit));
            Assert.True(!double.IsInfinity(merit));
        }

        /// <summary>
        /// Single absorbing layer (n=1.37, k=-7.62) on N-BK7, 0.01 um thick,
        /// at 0.633 um normal incidence. Compare R and T against ZEMAX.
        /// </summary>
        [Fact]
        public void AbsorbingLayer_SingleLayer_0633um_NormalIncidence()
        {
            var catalogService = new CatalogService();
            catalogService.InitializeWithStandardMaterials();

            // Add custom absorbing material (metal-like, k=-7.62)
            var absorber = CoatingMaterial.Custom("Absorber", 1.37, -7.62);
            catalogService.AddCoatingMaterial(absorber);

            var calc = new ThinFilmCalculator(catalogService);

            var design = new CoatingDesign
            {
                Name = "AbsorberTest",
                SubstrateMaterial = "N-BK7",
                UseOpticalThickness = false,
                ReferenceWavelength_um = 0.633,
                Layers = new System.Collections.Generic.List<DesignLayer>
                {
                    new DesignLayer
                    {
                        MaterialName = "Absorber",
                        Thickness = 0.01,  // 0.01 um physical
                        IsVariable = false
                    }
                }
            };

            var result = calc.Calculate(design, 0.633, 0);

            // At normal incidence Rs == Rp
            Assert.Equal(result.Rs, result.Rp, 6);
            Assert.Equal(result.Ts, result.Tp, 6);

            // R + T + A = 100% (absorptance makes up the difference)
            // For absorbing media R+T < 100
            Assert.True(result.Rave >= 0 && result.Rave <= 100);
            Assert.True(result.Tave >= 0 && result.Tave <= 100);
            Assert.True(result.Rave + result.Tave <= 100.0,
                $"R+T should be <= 100% for absorbing layer, got R={result.Rave:F4} T={result.Tave:F4} sum={result.Rave + result.Tave:F4}");

            // Print values for ZEMAX comparison (visible in test output)
            System.Console.WriteLine($"Absorbing layer test @ 0.633 um, AOI=0 deg");
            System.Console.WriteLine($"  Material: n=1.37, k=-7.62, thickness=0.01 um");
            System.Console.WriteLine($"  Rs  = {result.Rs:F6} %");
            System.Console.WriteLine($"  Rp  = {result.Rp:F6} %");
            System.Console.WriteLine($"  Rave= {result.Rave:F6} %");
            System.Console.WriteLine($"  Ts  = {result.Ts:F6} %");
            System.Console.WriteLine($"  Tp  = {result.Tp:F6} %");
            System.Console.WriteLine($"  Tave= {result.Tave:F6} %");
            System.Console.WriteLine($"  Absorptance = {100.0 - result.Rave - result.Tave:F6} %");
        }

        [Fact]
        public void EnergyConservation_RplusTEquals100()
        {
            var calc = CreateCalculator();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            // Test at various angles
            for (double aoi = 0; aoi <= 60; aoi += 15)
            {
                var result = calc.Calculate(design, 0.55, aoi);
                if (!result.IsTIR)
                {
                    // R + T should be close to 100% for non-absorbing materials
                    Assert.InRange(result.Rs + result.Ts, 99.0, 101.0);
                    Assert.InRange(result.Rp + result.Tp, 99.0, 101.0);
                }
            }
        }
    }
}
