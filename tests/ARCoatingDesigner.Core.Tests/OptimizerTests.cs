using System;
using System.Linq;
using ARCoatingDesigner.Core.Calculations;
using ARCoatingDesigner.Core.Catalogs;
using ARCoatingDesigner.Core.Models;
using ARCoatingDesigner.Core.Optimization;
using Xunit;

namespace ARCoatingDesigner.Core.Tests
{
    public class OptimizerTests
    {
        private (ThinFilmCalculator calc, CoatingOptimizer opt) CreateOptimizer()
        {
            var catalogService = new CatalogService();
            catalogService.InitializeWithStandardMaterials();
            var calc = new ThinFilmCalculator(catalogService);
            var opt = new CoatingOptimizer(calc);
            return (calc, opt);
        }

        [Fact]
        public void Optimize_SingleLayerQWOT_ReducesMerit()
        {
            var (calc, opt) = CreateOptimizer();
            var design = CoatingDesign.CreateDefaultSingleLayer();

            // Start with a non-optimal thickness
            design.Layers[0].Thickness = 0.10; // Off from optimal 0.25
            design.Layers[0].MinThickness = 0.01;
            design.Layers[0].MaxThickness = 1.0;

            double initialMerit = calc.CalculateMerit(design);
            var result = opt.Optimize(design, maxIterations: 50);

            Assert.True(result.Success);
            Assert.True(result.FinalMerit <= initialMerit);
        }

        [Fact]
        public void Optimize_NoVariableLayers_ReturnsFalse()
        {
            var (calc, opt) = CreateOptimizer();
            var design = CoatingDesign.CreateDefaultSingleLayer();
            design.Layers[0].IsVariable = false;

            var result = opt.Optimize(design);

            Assert.False(result.Success);
            Assert.Contains("No variable layers", result.Message);
        }

        [Fact]
        public void Optimize_NoTargets_ReturnsFalse()
        {
            var (calc, opt) = CreateOptimizer();
            var design = CoatingDesign.CreateDefaultSingleLayer();
            design.MeritTargets.Clear();

            var result = opt.Optimize(design);

            Assert.False(result.Success);
            Assert.Contains("No active merit targets", result.Message);
        }

        [Fact]
        public void MeritTarget_Equal_ComputesMerit()
        {
            var target = new MeritTarget
            {
                CompareType = CompareType.Equal,
                TargetValue = 0,
                Weight = 1.0,
                UseTarget = true
            };

            double merit = target.ComputeMerit(2.0);
            Assert.Equal(4.0, merit); // (2-0)^2 * 1.0 = 4.0
        }

        [Fact]
        public void MeritTarget_LessOrEqual_NoPenaltyWhenBelow()
        {
            var target = new MeritTarget
            {
                CompareType = CompareType.LessOrEqual,
                TargetValue = 5.0,
                Weight = 1.0,
                UseTarget = true
            };

            double merit = target.ComputeMerit(3.0);
            Assert.Equal(0.0, merit); // 3 <= 5, no penalty
        }

        [Fact]
        public void MeritTarget_LessOrEqual_PenaltyWhenAbove()
        {
            var target = new MeritTarget
            {
                CompareType = CompareType.LessOrEqual,
                TargetValue = 5.0,
                Weight = 1.0,
                UseTarget = true
            };

            double merit = target.ComputeMerit(7.0);
            Assert.Equal(4.0, merit); // (7-5)^2 * 1.0 = 4.0
        }

        [Fact]
        public void MeritTarget_GreaterOrEqual_NoPenaltyWhenAbove()
        {
            var target = new MeritTarget
            {
                CompareType = CompareType.GreaterOrEqual,
                TargetValue = 5.0,
                Weight = 1.0,
                UseTarget = true
            };

            double merit = target.ComputeMerit(7.0);
            Assert.Equal(0.0, merit);
        }

        [Fact]
        public void MeritTarget_GreaterOrEqual_PenaltyWhenBelow()
        {
            var target = new MeritTarget
            {
                CompareType = CompareType.GreaterOrEqual,
                TargetValue = 5.0,
                Weight = 1.0,
                UseTarget = true
            };

            double merit = target.ComputeMerit(3.0);
            Assert.Equal(4.0, merit); // (5-3)^2 * 1.0 = 4.0
        }

        [Fact]
        public void MeritTarget_DisabledTarget_ReturnsZero()
        {
            var target = new MeritTarget
            {
                CompareType = CompareType.Equal,
                TargetValue = 0,
                Weight = 1.0,
                UseTarget = false
            };

            double merit = target.ComputeMerit(10.0);
            Assert.Equal(0.0, merit);
        }

        [Fact]
        public void CoatingDesign_Clone_IsIndependent()
        {
            var design = CoatingDesign.CreateDefaultSingleLayer();
            var clone = design.Clone();

            clone.Layers[0].Thickness = 999;
            Assert.NotEqual(999, design.Layers[0].Thickness);
        }

        [Fact]
        public void CoatingDesign_SetVariableThicknesses_UpdatesLayers()
        {
            var design = CoatingDesign.CreateDefaultVCoat();
            design.SetVariableThicknesses(new double[] { 0.15, 0.35 });

            Assert.Equal(0.15, design.Layers[0].Thickness);
            Assert.Equal(0.35, design.Layers[1].Thickness);
        }
    }
}
