using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Linq;
using System.Security;
using System.Windows.Controls;

using StockSharp.Algo.Candles;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Quik;
using StockSharp.Xaml.Charting;

using Ecng.Common;
using Ecng.Xaml;
using Ecng.Collections;


namespace WpfApplication1
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private QuikTrader _trader;
        private Security _sec;
        private Order _order;
        private Portfolio _port;
        private decimal _pr;

        private CandleSeries _candleSeries;
        private CandleManager _candleManager;
        private TimeSpan _timeSpan;
        private ChartArea _area;
        private ChartCandleElement _candlesElem;

        private ExchangeBoard _board=ExchangeBoard.MicexJunior;
        private string _code = "LKOH";
        Object _obj = new object();
        private Chart _chart = new Chart();
        private readonly Dictionary<CandleSeries, ChartWindow> _chartWindows = new Dictionary<CandleSeries, ChartWindow>();
        
        public MainWindow()
        {
            InitializeComponent();
            
        }

        ChartWindow chartw = new ChartWindow();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_trader == null || _trader.ConnectionState == ConnectionStates.Connected)
                {
                    _trader.Dispose();
                }

                base.OnClosing(e);
            }
            catch (Exception)
            {
                MessageBox.Show("Ошибка отключения!");
            }
        }
        
        private ExchangeBoard GetExchengeBoardPort(Portfolio portfolio)
        {
            return _board = portfolio.Board == ExchangeBoard.Forts ? ExchangeBoard.Forts : ExchangeBoard.MicexJunior;
        }

        private void ConnectionClick(object sender, RoutedEventArgs e)
        {
            //Создаем подключение к Quik 
            if (_trader == null)
            {
                _trader = new QuikTrader
                    {
                        LuaFixServerAddress = "127.0.0.1:5001".To<EndPoint>(),
                        LuaLogin = "quik",
                        LuaPassword = "quik".To<SecureString>()
                    };

                //Подписываемся на события получения новых портфелей и инструментов
                _trader.NewPortfolios += portfolios=>this.GuiAsync(()=>
                    { PorfolioComboBox.ItemsSource = _trader.Portfolios; });
                _trader.NewSecurities += securities => this.GuiAsync(() =>
                    { SecurityComboBox.ItemsSource = _trader.Securities.Where(s=>s.Board==_board); });
                
                //Подключаемся к терминалу
                _trader.Connect();
            }

            _candleManager=new CandleManager();
            _candleManager.Processing += DrowCandle;

            ConnectButton.IsEnabled = false;
        }

        private void DrowCandle(CandleSeries series, Candle candle)
        {
            var wnd = _chartWindows.TryGetValue(series);

            if (wnd!=null)
                wnd.Chart.Draw((ChartCandleElement)wnd.Chart.Areas[0].Elements[0], candle);
        }

        private void BayBestBidClick(object sender, RoutedEventArgs e)
        {
            try
            {
                //Получаем выбранный в комбобоксе портфель
                _port = (Portfolio) PorfolioComboBox.SelectedItem;
                //Получаем цену BestBid по выбранному в комбобокс инчтрументу
                _pr = _trader.GetMarketDepth(_sec).BestBid.Price;
                
                if (_trader != null)
                {
                    //Создаем ордер и параметризируем его необходимыми данными
                    _order = new Order
                        {
                            Connector = _trader,
                            Portfolio = _port,
                            Security = _sec,
                            Volume = 1,
                            Direction = Sides.Buy,
                            Price = _pr,
                        };
                    //Регистрируем ордер
                    _trader.RegisterOrder(_order);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Нет котировок по инструменту" + ((Security)SecurityComboBox.SelectedItem).Code);
            }
        }

        private void SellBestAsk(object sender, RoutedEventArgs e)
        {
            try
            {
                //Получаем выбранный в комбобоксе портфель
                _port = (Portfolio) PorfolioComboBox.SelectedItem;
                //Получаем цену BestAsk по выбранному в комбобокс инчтрументу
                _pr = _trader.GetMarketDepth(_sec).BestAsk.Price;

                if (_trader != null)
                {
                    //Создаем ордер и параметризируем его необходимыми данными
                    _order = new Order
                        {
                            Connector = _trader,
                            Portfolio = _port,
                            Security = _sec,
                            Volume = 1,
                            Direction = Sides.Sell,
                            Price = _pr
                        };
                    //Регистрируем ордер
                    _trader.RegisterOrder(_order);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Нет котировок по инструменту" + ((Security)SecurityComboBox.SelectedItem).Code);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //Удаляем все неисполнеенные ордеры
            _trader.CancelOrders();
        }
        
        //По этому событию сразу происходит регистрация на получение котировок по выбранному в комбобокс инструменту
        private void RegisterSelectionChenged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SecurityComboBox.SelectedIndex = SecurityComboBox.SelectedIndex == -1 ? 0 : SecurityComboBox.SelectedIndex;

            //Получаем инструмент из выбранного в комбобокс
            _code = ((Security)SecurityComboBox.SelectedItem).Code;
            _sec = _trader.Securities.First(s => s.Code ==_code );

            //Регистрируем инструмент на получение котировок 
            _trader.RegisterMarketDepth(_sec); 
        }

        private void GetCandelsClick1(object sender, RoutedEventArgs e)
        {
            _timeSpan=TimeSpan.FromMinutes(5);
            _candleSeries=new CandleSeries(typeof(TimeFrameCandle), _sec, _timeSpan);

            _chartWindows.SafeAdd(_candleSeries, kay =>
                {
                    var wnd = new ChartWindow();
                    wnd.MakeHideable();

                    _area=new ChartArea();
                    wnd.Chart.Areas.Add(_area);

                    _candlesElem=new ChartCandleElement();
                    _area.Elements.Add(_candlesElem);

                    return wnd;
                }).Show();

            _candleManager.Start(_candleSeries);

/*            _area = new ChartArea();
            chartw.Chart.Areas.Add(_area);

            _candlesElem=new ChartCandleElement();
            _area.Elements.Add(_candlesElem);



            //Создаем свечки
            var candleManager = new CandleManager(_trader);
            _timeSpan = TimeSpan.FromMinutes(5);
            _candleSeries=new CandleSeries(typeof(TimeFrameCandle), _sec, _timeSpan);
            _candleSeries.ProcessCandle+=candle=>this.GuiAsync(() =>
                {
                    if (candle.State==CandleStates.Finished)
                        chartw.Chart.Draw(_candlesElem, candle);
                });

            candleManager.Start(_candleSeries);
            chartw.Show();   */
        }

        private void PorfolioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Определяем тип рынка портфеля и заполняем комбобокс инструментами для этого портфеля
            _board = GetExchengeBoardPort((Portfolio)PorfolioComboBox.SelectedItem);
            SecurityComboBox.ItemsSource = _trader.Securities.Where(s => s.Board == _board);
            
        }
    }
}
