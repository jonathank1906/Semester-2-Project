using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Heatwise.Models;

public class Optimizer
{
    private readonly AssetManager _assetManager;
   
    public Optimizer(AssetManager assetManager)
    {
        _assetManager = assetManager;
    }

    public List<HeatProductionResult> CalculateOptimalHeatProduction(List<(DateTime timestamp, double heatDemand)> heatDemandIntervals, OptimizationMode optimizationMode, List<(DateTime timestamp, double value)> electricityPriceData)
    {
        var results = heatDemandIntervals
            .SelectMany(interval => ProcessInterval(interval, optimizationMode, electricityPriceData))
            .ToList();

        return results;
    }

    private double CalculateNetCost(AssetModel asset, double electricityPrice)
    {
        double netCost;
        if (asset.ProducesElectricity)
        {
            double electricityPerMWhHeat = asset.MaxElectricity / asset.MaxHeat; // 0.743 
            netCost = asset.CostPerMW - (electricityPrice * electricityPerMWhHeat);
        }
        else if (asset.ConsumesElectricity)
        {
            double electricityPerMWhHeat = Math.Abs(asset.MaxElectricity / asset.MaxHeat); // 1
            netCost = asset.CostPerMW + electricityPrice * electricityPerMWhHeat;
        }
        else
        {
            netCost = asset.CostPerMW;
        }
        return netCost;
    }
    
    private List<AssetModel> SortByPriority(List<AssetModel> assets, double electricityPrice, OptimizationMode optimisationMode)
    {
        if (optimisationMode == OptimizationMode.CO2)
        {
            return assets
                .Where(a => a.IsActive && a.HeatProduction > 0)
                .OrderBy(a => a.EmissionsPerMW) // Prioritize lower emissions
                .ToList();
        }

        // Default to cost optimization
        return assets
            .Where(a => a.IsActive && a.HeatProduction > 0)
            .OrderBy(a => CalculateNetCost(a, electricityPrice))
            .ToList();
    }

    private List<HeatProductionResult> ProcessInterval((DateTime timestamp, double heatDemand) interval, OptimizationMode optimisationMode, List<(DateTime timestamp, double value)> electricityPriceData)
    {
        var timestamp = interval.timestamp;
        var heatDemand = interval.heatDemand;

        // Find the electricity price for the current timestamp
        var electricityPrice = electricityPriceData
            .FirstOrDefault(p => p.timestamp == timestamp).value;

        if (electricityPrice == default)
        {
            Debug.WriteLine($"WARNING: No electricity price found for timestamp {timestamp}. Defaulting to 0.");
            electricityPrice = 0.0;
        }

        // Sort assets by priority based on the electricity price
        var prioritizedAssets = SortByPriority(_assetManager.CurrentAssets, electricityPrice, optimisationMode);

        // Allocate heat demand to the prioritized assets
        return AllocateHeat(timestamp, heatDemand, prioritizedAssets, electricityPrice);
    }

    private List<HeatProductionResult> AllocateHeat(DateTime timestamp, double heatDemand, List<AssetModel> prioritizedAssets, double electricityPrice)
    {
        var results = new List<HeatProductionResult>();
        double remainingDemand = heatDemand;

        foreach (var asset in prioritizedAssets)
        {
            if (remainingDemand <= 0) break;

            double allocation = Math.Min(asset.HeatProduction, remainingDemand);
            remainingDemand -= allocation;

            double netCostPerMWh = CalculateNetCost(asset, electricityPrice);
            double productionCost = allocation * netCostPerMWh;

            // Calculate electricity consumption or production
            double electricityConsumption = 0;
            double electricityProduction = 0;
            if (asset.ConsumesElectricity)
            {
                electricityConsumption = allocation * Math.Abs(asset.MaxElectricity / asset.MaxHeat);
            }
            else if (asset.ProducesElectricity)
            {
                electricityProduction = allocation * (asset.MaxElectricity / asset.MaxHeat);
            }

            // Calculate oil and gas consumption
            double oilConsumption = allocation * asset.OilConsumption;
            double gasConsumption = allocation * asset.GasConsumption;

            results.Add(new HeatProductionResult
            {
                AssetName = asset.Name,
                HeatProduced = allocation,
                ProductionCost = productionCost,
                Emissions = allocation * asset.EmissionsPerMW,
                Timestamp = timestamp,
                PresetId = asset.PresetId,
                ElectricityConsumption = electricityConsumption,
                ElectricityProduction = electricityProduction,
                OilConsumption = oilConsumption, 
                GasConsumption = gasConsumption 
            });
        }

        if (remainingDemand > 0)
        {
            results.Add(new HeatProductionResult
            {
                AssetName = "Unmet Demand",
                HeatProduced = remainingDemand,
                ProductionCost = 0,
                Emissions = 0,
                Timestamp = timestamp,
                PresetId = -1,
                ElectricityConsumption = 0,
                ElectricityProduction = 0,
                OilConsumption = 0, 
                GasConsumption = 0  
            });
        }
        return results;
    }
}