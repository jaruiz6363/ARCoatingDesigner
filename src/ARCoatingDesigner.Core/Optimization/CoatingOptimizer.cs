using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MathNet.Numerics.LinearAlgebra;
using ARCoatingDesigner.Core.Models;
using ARCoatingDesigner.Core.Calculations;

namespace ARCoatingDesigner.Core.Optimization
{
    public class OptimizationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public double InitialMerit { get; set; }
        public double FinalMerit { get; set; }
        public int Iterations { get; set; }
        public double[] OptimizedThicknesses { get; set; } = Array.Empty<double>();
    }

    public class CoatingOptimizer
    {
        private readonly ThinFilmCalculator _calculator;
        private readonly Random _random = new Random();

        public CoatingOptimizer(ThinFilmCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        public OptimizationResult Optimize(CoatingDesign design, int maxIterations = 200,
            double initialMu = 1e-3, double gradientTolerance = 1e-10,
            double stepTolerance = 1e-10, double functionTolerance = 1e-10)
        {
            var result = new OptimizationResult();

            try
            {
                var variableLayers = design.GetVariableLayers().ToList();
                if (variableLayers.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No variable layers to optimize";
                    return result;
                }

                var activeTargets = design.GetActiveTargets().ToList();
                if (activeTargets.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No active merit targets";
                    return result;
                }

                double[] initialGuess = design.GetVariableThicknesses();
                double[] lowerBound = design.GetLowerBounds();
                double[] upperBound = design.GetUpperBounds();

                result.InitialMerit = _calculator.CalculateMerit(design);

                var optimized = RunLM(design.Clone(), activeTargets, initialGuess, lowerBound, upperBound,
                    maxIterations, initialMu, gradientTolerance, stepTolerance, functionTolerance,
                    out int iterations, CancellationToken.None);

                design.SetVariableThicknesses(optimized);
                result.OptimizedThicknesses = optimized;
                result.FinalMerit = _calculator.CalculateMerit(design);
                result.Iterations = iterations;
                result.Success = true;
                result.Message = $"Optimization completed ({iterations} iter). Merit: {result.InitialMerit:F6} -> {result.FinalMerit:F6}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Optimization failed: {ex.Message}";
            }

            return result;
        }

        public OptimizationResult GlobalOptimize(CoatingDesign design, int maxTrials = 50, int maxIterationsPerTrial = 200,
            double initialMu = 1e-3, double gradientTolerance = 1e-10,
            double stepTolerance = 1e-10, double functionTolerance = 1e-10,
            IProgress<(int trial, int total, double bestMerit)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new OptimizationResult();
            double[] bestThicknesses = design.GetVariableThicknesses();
            int totalIterations = 0;
            int trial = 0;

            try
            {
                var variableLayers = design.GetVariableLayers().ToList();
                if (variableLayers.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No variable layers to optimize";
                    return result;
                }

                var activeTargets = design.GetActiveTargets().ToList();
                if (activeTargets.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No active merit targets";
                    return result;
                }

                result.InitialMerit = _calculator.CalculateMerit(design);

                double[] lowerBound = design.GetLowerBounds();
                double[] upperBound = design.GetUpperBounds();

                double bestMerit = double.MaxValue;

                for (trial = 0; trial < maxTrials; trial++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var trialDesign = design.Clone();

                    var varLayers = trialDesign.GetVariableLayers().ToList();
                    var startPoint = new double[varLayers.Count];
                    for (int i = 0; i < varLayers.Count; i++)
                    {
                        double range = upperBound[i] - lowerBound[i];
                        startPoint[i] = lowerBound[i] + _random.NextDouble() * range;
                        varLayers[i].Thickness = startPoint[i];
                    }

                    try
                    {
                        var optimized = RunLM(trialDesign, activeTargets, startPoint, lowerBound, upperBound,
                            maxIterationsPerTrial, initialMu, gradientTolerance, stepTolerance, functionTolerance,
                            out int iterations, cancellationToken);
                        totalIterations += iterations;

                        trialDesign.SetVariableThicknesses(optimized);
                        double merit = _calculator.CalculateMerit(trialDesign);

                        if (merit < bestMerit)
                        {
                            bestMerit = merit;
                            bestThicknesses = optimized;
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { /* Skip failed trials */ }

                    progress?.Report((trial + 1, maxTrials, bestMerit));
                }

                design.SetVariableThicknesses(bestThicknesses);
                result.OptimizedThicknesses = bestThicknesses;
                result.FinalMerit = _calculator.CalculateMerit(design);
                result.Iterations = totalIterations;
                result.Success = true;
                result.Message = $"Global optimization ({trial} trials, {totalIterations} iter). Merit: {result.InitialMerit:F6} -> {result.FinalMerit:F6}";
            }
            catch (OperationCanceledException)
            {
                // Apply best result found so far
                design.SetVariableThicknesses(bestThicknesses);
                result.OptimizedThicknesses = bestThicknesses;
                result.FinalMerit = _calculator.CalculateMerit(design);
                result.Iterations = totalIterations;
                result.Success = true;
                result.Message = $"Global optimization stopped ({trial} trials). Merit: {result.InitialMerit:F6} -> {result.FinalMerit:F6}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Global optimization failed: {ex.Message}";
            }

            return result;
        }

        private double[] RunLM(CoatingDesign design, List<MeritTarget> activeTargets,
            double[] initialGuess, double[] lower, double[] upper, int maxIterations,
            double initialMu, double gradientTol, double stepTol, double funcTol,
            out int iterations, CancellationToken ct)
        {
            int nParams = initialGuess.Length;
            int nResiduals = activeTargets.Count;
            double[] x = (double[])initialGuess.Clone();
            iterations = 0;

            var variableIndices = new List<int>();
            for (int i = 0; i < design.Layers.Count; i++)
                if (design.Layers[i].IsVariable)
                    variableIndices.Add(i);

            double mu = initialMu;
            double delta = 1e-7;

            // Apply initial parameters and compute initial cost
            for (int i = 0; i < nParams; i++)
                design.Layers[variableIndices[i]].Thickness = x[i];

            double[] residuals = ComputeResiduals(design, activeTargets, nResiduals);
            double cost = DotProduct(residuals, residuals);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                ct.ThrowIfCancellationRequested();
                iterations++;

                // Compute Jacobian (finite differences)
                double[,] J = new double[nResiduals, nParams];
                for (int p = 0; p < nParams; p++)
                {
                    double orig = design.Layers[variableIndices[p]].Thickness;
                    double h = Math.Max(delta, Math.Abs(orig) * delta);
                    design.Layers[variableIndices[p]].Thickness = orig + h;

                    double[] perturbedResiduals = ComputeResiduals(design, activeTargets, nResiduals);
                    for (int i = 0; i < nResiduals; i++)
                        J[i, p] = (perturbedResiduals[i] - residuals[i]) / h;

                    design.Layers[variableIndices[p]].Thickness = orig;
                }

                // Compute J^T * J and J^T * r
                var JtJ = new double[nParams, nParams];
                var Jtr = new double[nParams];
                double gradNorm = 0;

                for (int i = 0; i < nParams; i++)
                {
                    for (int j = 0; j < nParams; j++)
                    {
                        double sum = 0;
                        for (int k = 0; k < nResiduals; k++)
                            sum += J[k, i] * J[k, j];
                        JtJ[i, j] = sum;
                    }

                    double rsum = 0;
                    for (int k = 0; k < nResiduals; k++)
                        rsum += J[k, i] * residuals[k];
                    Jtr[i] = rsum;
                    gradNorm += Jtr[i] * Jtr[i];
                }
                gradNorm = Math.Sqrt(gradNorm);

                // Check gradient convergence
                if (gradNorm < gradientTol)
                    break;

                // Try to find a good step with adaptive damping
                bool stepAccepted = false;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    // Solve (J^T*J + mu*diag(J^T*J)) * step = -J^T*r
                    var A = Matrix<double>.Build.DenseOfArray(JtJ);
                    for (int i = 0; i < nParams; i++)
                        A[i, i] += mu * Math.Max(JtJ[i, i], 1e-6);
                    var b = Vector<double>.Build.DenseOfArray(Jtr);

                    Vector<double> step;
                    try { step = A.Solve(-b); }
                    catch { mu *= 10; continue; }

                    // Check step size convergence
                    double stepNorm = 0;
                    double xNorm = 0;
                    for (int i = 0; i < nParams; i++)
                    {
                        stepNorm += step[i] * step[i];
                        xNorm += x[i] * x[i];
                    }
                    if (Math.Sqrt(stepNorm) < stepTol * (Math.Sqrt(xNorm) + stepTol))
                    {
                        stepAccepted = true;
                        break;
                    }

                    // Apply step with bounds clamping
                    var xNew = new double[nParams];
                    for (int i = 0; i < nParams; i++)
                        xNew[i] = Math.Max(lower[i], Math.Min(upper[i], x[i] + step[i]));

                    for (int i = 0; i < nParams; i++)
                        design.Layers[variableIndices[i]].Thickness = xNew[i];

                    double[] newResiduals = ComputeResiduals(design, activeTargets, nResiduals);
                    double newCost = DotProduct(newResiduals, newResiduals);

                    if (newCost < cost)
                    {
                        // Accept step
                        x = xNew;
                        residuals = newResiduals;

                        // Check function convergence
                        if (Math.Abs(cost - newCost) < funcTol * cost && iter > 0)
                        {
                            cost = newCost;
                            stepAccepted = true;
                            break;
                        }

                        cost = newCost;
                        mu *= 0.3333;
                        mu = Math.Max(mu, 1e-15);
                        stepAccepted = true;
                        break;
                    }
                    else
                    {
                        // Reject step, increase damping
                        mu *= 3.0;
                        mu = Math.Min(mu, 1e15);

                        // Restore parameters
                        for (int i = 0; i < nParams; i++)
                            design.Layers[variableIndices[i]].Thickness = x[i];
                    }
                }

                if (!stepAccepted)
                    break;
            }

            return x;
        }

        private double[] ComputeResiduals(CoatingDesign design, List<MeritTarget> activeTargets, int n)
        {
            var residuals = new double[n];
            for (int i = 0; i < n; i++)
            {
                var target = activeTargets[i];
                double val = _calculator.GetTargetValue(design, target);
                switch (target.CompareType)
                {
                    case CompareType.Equal:
                        residuals[i] = val - target.TargetValue;
                        break;
                    case CompareType.LessOrEqual:
                        residuals[i] = Math.Max(0, val - target.TargetValue);
                        break;
                    case CompareType.GreaterOrEqual:
                        residuals[i] = Math.Max(0, target.TargetValue - val);
                        break;
                }
                residuals[i] *= Math.Sqrt(target.Weight);
            }
            return residuals;
        }

        private static double DotProduct(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return sum;
        }
    }
}
