using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// PageNumberSelection.xaml 的交互逻辑
    /// </summary>
    public partial class PageNumberSelection : Page
    {
        public PageNumberSelection()
        {
            InitializeComponent();
            this.Visibility = Visibility.Collapsed;
            LeftListBox.SelectionChanged += (obt, e) =>
            {
                if (LeftListBox.SelectedIndex == -1) return;
                switch (LeftListBox.SelectedIndex)
                {
                    case 0:
                        WorkshopPage.Inst.TablePage.SwitchPage(WorkshopPage.Inst.NowPageType, 1);
                        break;
                    case 1:
                        if (Check(nowPage - 1, maxPage))
                            WorkshopPage.Inst.TablePage.SwitchPage(WorkshopPage.Inst.NowPageType, nowPage - 1);
                        break;
                }
                LeftListBox.SelectedIndex = -1;
            };
            RightListBox.SelectionChanged += (obt, e) =>
            {
                if (RightListBox.SelectedIndex == -1) return;
                switch (RightListBox.SelectedIndex)
                {
                    case 0:
                        if (Check(nowPage + 1, maxPage))
                            WorkshopPage.Inst.TablePage.SwitchPage(WorkshopPage.Inst.NowPageType, nowPage + 1);
                        break;
                    case 1:
                        WorkshopPage.Inst.TablePage.SwitchPage(WorkshopPage.Inst.NowPageType, maxPage);
                        break;
                }
                RightListBox.SelectedIndex = -1;
            };
            PageNumberTextBox.LostFocus += (sender, e) =>
            {
                PageNumberTextBoxRetuen();
            };
            PageNumberTextBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    PageNumberTextBoxRetuen();
                }
                if (e.Key == Key.Escape)
                {
                    PageNumberTextBox.Text = nowPage.ToString();
                }
            };
        }

        private void PageNumberTextBoxRetuen()
        {
            if (int.TryParse(PageNumberTextBox.Text, out int result) && Check(result, maxPage))
            {
                if (result != nowPage)
                    WorkshopPage.Inst.TablePage.SwitchPage(WorkshopPage.Inst.NowPageType, result);
            }
            else
            {
                PageNumberTextBox.Text = nowPage.ToString();
            }
        }

        private int nowPage = 1;
        private int maxPage = 1;

        public void Open(int nowPage, int maxPage)
        {
            this.nowPage = nowPage;
            this.maxPage = maxPage;
            PageNumberTextBox.Text = nowPage.ToString();
            MaxPageText.Text = maxPage.ToString();
            this.Visibility = Visibility.Visible;
        }
        public void Close()
        {
            this.Visibility = Visibility.Collapsed;
            PageNumberTextBox.Text = 1.ToString();
            MaxPageText.Text = 1.ToString();
        }

        public bool Check(int nowPage, int maxPage)
        {
            if (nowPage < 1 || nowPage > maxPage) return false;
            return true;
        }
    }
}
