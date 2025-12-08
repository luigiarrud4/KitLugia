using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;

namespace KitLugia.GUI.Controls
{
    public enum NotificationType { Info, Success, Error }

    public partial class LugiaToast : UserControl, INotifyPropertyChanged
    {
        public event Action<LugiaToast>? Dismissed;
        private bool _isDismissing = false;
        private DispatcherTimer _lifeTimer;

        // Controle de Spam (Auto Clicker)
        private long _lastAnimationTick = 0;

        public string NotificationId { get; private set; } = string.Empty;
        public NotificationType ToastType { get; private set; }

        private int _count = 1;

        public LugiaToast()
        {
            InitializeComponent();
            this.DataContext = this;

            // Vida útil (3s)
            _lifeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _lifeTimer.Tick += (s, e) => Dismiss();
        }

        public void SetContent(string title, string message, NotificationType type)
        {
            TxtTitle.Text = title.ToUpper();
            TxtMessage.Text = message;
            ToastType = type;
            SetValue(ToastTypeProperty, type);

            if (type == NotificationType.Info)
                NotificationId = "GENERIC_INFO_SLOT";
            else
                NotificationId = $"{type}|{title}|{message}";

            _lifeTimer.Start();
        }

        public void UpdateMessage(string newMessage)
        {
            TxtMessage.Text = newMessage;
            ResetTimer();
        }

        public void IncrementCounter()
        {
            _count++;
            TxtCount.Text = $"x{_count}";
            BadgeCounter.Visibility = Visibility.Visible;

            ResetTimer(); // Mantém a notificação viva

            // --- PROTEÇÃO CONTRA AUTO CLICKER (UI FREEZE) ---
            // Só executa a animação visual se passou 100ms desde a última vez.
            // Se clicar 50x em 1 seg, vai animar umas 10x só, mas contar todas.
            long currentTick = DateTime.Now.Ticks;
            if (currentTick - _lastAnimationTick > 1000000) // 100ms em Ticks
            {
                _lastAnimationTick = currentTick;

                // Animação leve de "Pop"
                var scaleTrans = new ScaleTransform(1, 1);
                BadgeCounter.RenderTransform = scaleTrans;

                var anim = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(150));
                scaleTrans.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scaleTrans.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }
        }

        private void ResetTimer()
        {
            if (_isDismissing) return;
            _lifeTimer.Stop();
            _lifeTimer.Start();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Dismiss();
        }

        public void Dismiss()
        {
            if (_isDismissing) return;
            _isDismissing = true;
            _lifeTimer.Stop();

            this.IsHitTestVisible = false; // Mouse passa através

            if (Resources["SlideOutAnimation"] is Storyboard sb)
            {
                sb.Completed -= OnSlideOutCompleted;
                sb.Completed += OnSlideOutCompleted;
                sb.Begin(this);
            }
            else
            {
                OnSlideOutCompleted(null, EventArgs.Empty);
            }
        }

        private void OnSlideOutCompleted(object? sender, EventArgs e)
        {
            this.Opacity = 0;
            this.Visibility = Visibility.Collapsed;
            Dismissed?.Invoke(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty ToastTypeProperty = DependencyProperty.Register(
            "ToastType", typeof(NotificationType), typeof(LugiaToast), new PropertyMetadata(NotificationType.Info));
    }
}