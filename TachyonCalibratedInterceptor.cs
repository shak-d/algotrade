using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp.Algo
{

    public class TachyonCalibratedInterceptor : QCAlgorithm
    {


        ExponentialMovingAverage ema200;
        ExponentialMovingAverage ema65;
        ExponentialMovingAverage ema15;
        ExponentialMovingAverage ema9;
        RelativeStrengthIndex rsi;
        RollingWindow<TradeBar> last30 = new RollingWindow<TradeBar>(6);
        bool traded = false;
        OrderTicket init;
        bool canTrade = true;
        int tradeType = 0;
        List<ExponentialMovingAverage> EMAs = new List<ExponentialMovingAverage>();
        bool farEMA = true;
        TradeBar stopBar;
        bool faremaOp = false;
        Symbol symbol;

        public override void Initialize()
        {
            SetStartDate(2019, 01, 14);
            SetEndDate(2019, 07, 29);
            SetCash(20000); 
           symbol = AddEquity("AAPL", Resolution.Second, null, true, 0, true).Symbol;
            SetWarmUp(60000);
            ema200 = EMA(symbol, 60000, Resolution.Second, Field.Close);
            ema65 = EMA(symbol, 19500, Resolution.Second, Field.Close);
            ema15 = EMA(symbol, 4500, Resolution.Second, Field.Close);
            ema9 = EMA(symbol, 2700, Resolution.Second, Field.Close);
            EMAs.Add(ema200);
            EMAs.Add(ema15);
            EMAs.Add(ema65);
            EMAs.Add(ema9);
            rsi = new RelativeStrengthIndex(14);

            var rsiConsolidator = new TradeBarConsolidator(300);
            RegisterIndicator(symbol, rsi, rsiConsolidator);
            SubscriptionManager.AddConsolidator(symbol, rsiConsolidator);

            var consolidator5Min = new TradeBarConsolidator(300);
            consolidator5Min.DataConsolidated += (sender, baseData) => handler5Min(sender, baseData);
            SubscriptionManager.AddConsolidator(symbol, consolidator5Min);

            var consolidator3Sec = new TradeBarConsolidator(20);// 20 NVDA || 20 BABA
            consolidator3Sec.DataConsolidated += (sender, baseData) => handler3Sec(sender, baseData);
            SubscriptionManager.AddConsolidator(symbol, consolidator3Sec);

            Chart intradayChart = new Chart("abc", ChartType.Overlay);
            Chart intradayChart1 = new Chart("abcRSI", ChartType.Overlay);
            intradayChart.AddSeries(new Series("Buy", SeriesType.Scatter));
            intradayChart.AddSeries(new Series("Sell", SeriesType.Scatter));
            AddChart(intradayChart);
            AddChart(intradayChart1);

            Schedule.On(DateRules.EveryDay(symbol), TimeRules.AfterMarketOpen(symbol, 1), () => {

                canTrade = true;

            });

            Schedule.On(DateRules.EveryDay(symbol), TimeRules.AfterMarketOpen(symbol, 40), () => {

                farEMA = false;

            });
        }

        public override void OnData(Slice slice)
        {
            if (IsWarmingUp || !ema200.IsReady || !canTrade) return;


            TradeBar bar = slice[symbol];

           // resistance(bar);
           // consolidation(bar);
          //  FarFromMA(bar);

            

        }

        private void handler3Sec(object sender, TradeBar baseData) {

            if (IsWarmingUp || !ema200.IsReady || !canTrade) return;

            FarFromMA(baseData);
            if (faremaOp) faremaOp = false;
        }

        private void FarFromMA(TradeBar bar) {

            if (!traded)
            {

                ExponentialMovingAverage closest = ema9;
                foreach (ExponentialMovingAverage ema in EMAs)
                {

                    if (absolute(bar.Close - closest) > absolute(bar.Close - ema))
                    {

                        closest = ema;

                    }

                }
                if (rsi >= 80 &&
                   bar.Open - closest >= 1.0m &&
                   bar.Open < last30[0].High &&
                   bar.Open > bar.Close &&
                   farEMA &&
                   faremaOp)
                {

                   init = MarketOrder(symbol, -100, false, "FFMA");
                    stopBar = last30[0];
                    traded = true;
                    tradeType = 2;

                }

            }

            else if (tradeType == 2 && traded) {

                if (bar.Close <= init.AverageFillPrice - 0.75m)
                {

                    MarketOrder(symbol, 100, false, "profit");
                    traded = false;

                }
                else if (bar.Close >= stopBar.High + 0.15m||
                         bar.Time.Subtract(init.Time).TotalMinutes >= 15) {

                    MarketOrder(symbol, 100, false, "stop");
                    farEMA = false;
                    traded = false;

                }

            }

        }

        private void resistance(TradeBar bar) {

            if (!traded)
            {

                TradeBar highest = getHighest();

                //Resistance
                if (ema200 - highest.Open >= 0.5m &&
                    ema200 - highest.Open <= 2.0m &&
                    bar.Time.Day == highest.Time.Day &&
                    AllBelowEMA() &&
                    bar.Close >= ema200
                    )
                {

                    init = MarketOrder(symbol, -100, false, "init res.");
                    traded = true;
                    tradeType = 0;
                    Debug("init " + init.AverageFillPrice);

                }

            }

            else if (tradeType == 0 && traded)
            {

                if (bar.Close >= init.AverageFillPrice + 0.5m || bar.Time.Subtract(init.Time).TotalMinutes >= 30)
                {

                    OrderTicket ticket = MarketOrder(symbol, 100, false, "stop");
                    traded = false;
                   // Debug("Stop " + ticket.AverageFillPrice);
                }

                else if (bar.Close <= init.AverageFillPrice - 1.0m)
                {

                    OrderTicket ticket = MarketOrder(symbol, 100, false, "profit");
                    traded = false;
                   // Debug("Profit" + ticket.AverageFillPrice);

                }

            }

        }

        private void consolidation(TradeBar bar) {

            if (!traded)
            {

                TradeBar lowest = getLowest();

                if (lowest.Open - ema200 >= 0.5m &&
                    lowest.Open - ema200 <= 1.0m &&
                    bar.Time.Day == lowest.Time.Day &&
                    AllAboveEMA() &&
                    bar.Close <= ema200)
                {

                    init = MarketOrder(symbol, 100, false, "init con.");
                    traded = true;
                    tradeType = 1;
                }
            }
            else if (tradeType == 1 && traded)
            {

                if (bar.Close <= init.AverageFillPrice - 0.5m || bar.Time.Subtract(init.Time).TotalMinutes >= 30)
                {

                    MarketOrder(symbol, -100, false, "stop");
                    traded = false;

                }

                else if (bar.Close >= init.AverageFillPrice + 1.0m)
                {

                    MarketOrder(symbol, -100, false, "profit");
                    traded = false;

                }

            }

            

        }

        private bool AllBelowEMA() { 

            foreach (TradeBar bar in last30) {

                if (bar.Close - ema200 > 0)
                    return false;

            }

            return true;

        }

        private bool AllAboveEMA() {

            foreach (TradeBar bar in last30) {

                if (ema200 - bar.Close > 0)
                    return false;

            }

            return true;

        }

        private void handler5Min(object sender, TradeBar baseData)
        {
            last30.Add(baseData);
            Plot("EMA", "EMA Sgnal", ema200);
            Plot("stock", "price", baseData.Close);
            if (IsWarmingUp || !ema200.IsReady || !canTrade) return;
            Plot("abc", symbol, baseData.Price);
            Plot("abc", ema200);
            Plot("abc", ema65);
            Plot("abc", ema15);
            Plot("abc", ema9);
            Plot("abcRSI", rsi);
            faremaOp = true;

            if (tradeType == 2 && traded) {



            }
        }

        private TradeBar getHighest() {

            return last30[5];

            TradeBar bar = last30[5];

            foreach (TradeBar tradebar in last30) {

                if (tradebar.Open > bar.Open)
                {
                    bar = tradebar;
                    Debug("found");
                }

            }

            return bar;
        }
        private TradeBar getLowest() {

            TradeBar bar = last30[5];

            foreach (TradeBar tradeBar in last30) {

                if (tradeBar.Open < bar.Open) {

                    bar = tradeBar;

                }
               
            }
            return bar;
        }

        

        public override void OnEndOfDay(string symbol)
        {
            canTrade = false;
            traded = false;
            farEMA = true;
            Liquidate(symbol);
            
        }

        private decimal absolute(decimal value) {

            if (value < 0)
                return value * (-1);
            else
                return value;

        }

    }
}