using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StockSharp.Algo;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Candles;


namespace WpfApplication1
{
    class BollingerStrategy:Strategy
    {
        private readonly BollingerBands _bands;
        private readonly CandleSeries _series;
        
        private BollingerStrategy(BollingerBands bands, CandleSeries series)
        {
            _bands = bands;
            _series = series;
        }
        
        private void MainAlgorithm(Candle candle)
        {
            _bands.Process(candle.ClosePrice);
            var timeFrame = (TimeSpan) candle.Arg;
            var time = timeFrame.GetCandleBounds(Security.LastChangeTime).Min - timeFrame;
            if (candle.OpenTime>=time && _bands.IsFormed)
            {
                //Приходит только последняя свечка
                if (candle.ClosePrice>=_bands.UpBand.GetCurrentValue())
                {
                    this.ClosePosition();
                    RegisterOrder(this.SellAtMarket());
                }
                else if (candle.ClosePrice <= _bands.LowBand.GetCurrentValue())
                {
                    this.ClosePosition();
                    RegisterOrder(this.BuyAtMarket());
                }
            }
        }

        protected override void OnStarted()
        {
            _series.WhenCandlesFinished()
                   .Do(MainAlgorithm)
                   .Apply(this);

            base.OnStarted();
        }
    }
}
