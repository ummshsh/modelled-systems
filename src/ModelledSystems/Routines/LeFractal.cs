﻿using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using ChaosSoft.Core.DataUtils;
using ChaosSoft.NumericalMethods.Equations;
using ChaosSoft.NumericalMethods.Lyapunov;

namespace ModelledSystems.Routines;

internal class LeFractal : Routine
{
    private readonly int _paramIndex;
    private readonly string _sequence;
    private readonly double[,] _arr;

    private readonly TaskProgress _progress;
    private readonly int _iterations;
    private readonly Parameter _parameter;
    private readonly double _step;

    public LeFractal(string outDir, SystemParameters systemParameters, int paramIndex, int iterations, string sequence) 
        : base(outDir, systemParameters)
    {
        _paramIndex = paramIndex;
        _sequence = sequence;

        _parameter = SysParameters.ListParameters[paramIndex];
        _iterations = iterations;
        _step = (_parameter.End - _parameter.Start) / _iterations;
        _progress = new TaskProgress(_iterations * _iterations);

        _arr = new double[_iterations, _iterations];
        Matrix.FillWith(_arr, -1);
    }

    public override void Run()
    {
        Parallel.For(0, _iterations * _iterations, z => Func(z));
        GetImage();
    }

    private void GetImage()
    {
        var plt = new ScottPlot.Plot(Size.Width, Size.Height);
        
        plt.XAxis.Label(_parameter.Name);
        plt.YAxis.Label(_parameter.Name);

        //int maxPositiveLeIndex = (int)Matrixes.Max(_arr);
        //int minLeIndex = (int)Matrixes.Min(_arr);
        MakeGradient();

        var hm = plt.AddHeatmap(_arr, ScottPlot.Drawing.Colormap.Topo, lockScales: false);
        //var cb = plt.AddColorbar(hm);
        hm.Smooth = true;
        plt.Margins(0, 0);


        //double[] ticks = Arrays.GenerateUniformArray(maxPositiveLeIndex + 1, minLeIndex, 1d);

        //cb.SetTicks(
        //    ticks,
        //    ticks.Select(t => t.ToString()).ToArray(), 
        //    min: minLeIndex, 
        //    max: maxPositiveLeIndex);

        plt.XTicks(new double[] { 0, _iterations }, new string[] { _parameter.Start.ToString(), _parameter.End.ToString() });
        plt.YTicks(new double[] { 0, _iterations }, new string[] { _parameter.Start.ToString(), _parameter.End.ToString() });

        plt.SaveFig(Path.Combine(OutDir, SysParameters.SystemName + "_lyapunov_fractal.png"));
    }

    public void Func(int z)
    {
        int x = z / _iterations;
        int y = z % _iterations;

        double firstValue = _parameter.Start + x * _step;
        double secondValue = _parameter.Start + y * _step;
        double[] vars = new double[SysParameters.Defaults.Length];
        Array.Copy(SysParameters.Defaults, vars, vars.Length);

        SystemBase equations = GetLinearizedSystemEquations(vars);
        Type solverType = GetSolverType(SysParameters.Solver);
        double eqStep = SysParameters.Step;
        long totIter = (long)(SysParameters.ModellingTime / eqStep);

        LleBenettin benettin = new LleBenettin(equations, solverType, eqStep, totIter);

        for (int i = 0; i < totIter; i++)
        {
            double val = _sequence.ToLowerInvariant()[i % _sequence.Length] == 'a' ? firstValue : secondValue;
            //TODO determine vars

            vars[_paramIndex] = val;
            equations.SetParameters(vars);
            benettin.MakeIteration();
        }

        _arr[_iterations - 1 - y, x] = benettin.Result;
        _progress.Iterate();
    }

    private void MakeGradient()
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int x = 0; x < _iterations; x++)
        {
            for (int y = 0; y < _iterations; y++)
            {
                if (double.IsInfinity(_arr[y, x]))
                {
                    continue;
                }

                if (_arr[y, x] < min)
                {
                    min = _arr[y, x];
                }

                if (_arr[y, x] > max)
                {
                    max = _arr[y, x];
                }
            }
        }

        double absMin = Math.Abs(min);
        double coeff = absMin > max ? absMin / max : max / absMin;
        double posCoeff = absMin > max ? 1 : max / absMin;
        double negCoeff = max > absMin ? 1 : absMin / max;

        for (int x = 0; x < _iterations; x++)
        {
            for (int y = 0; y < _iterations; y++)
            {
                if (double.IsPositiveInfinity(_arr[y, x]))
                {
                    _arr[y, x] = max / posCoeff;
                } 
                else if (double.IsNegativeInfinity(_arr[y, x]))
                {
                    _arr[y, x] = min / negCoeff;
                }
                else if (_arr[y, x] == -1)
                {
                    _arr[y, x] = max / posCoeff;
                }
                else if (_arr[y, x] > 0)
                {
                    _arr[y, x] /= posCoeff;
                }
                else if (_arr[y, x] < 0)
                {
                    _arr[y, x] /= negCoeff;
                }
            }
        }

    }

    /*
     * For shades, multiply each component by 1/4, 1/2, 3/4, etc., of its previous value. 
     * The smaller the factor, the darker the shade.
     *
     * For tints, calculate (255 - previous value), multiply that by 1/4, 1/2, 3/4, etc. (the greater the factor, the lighter the tint), 
     * and add that to the previous value (assuming each.component is a 8-bit integer).
    */
}
