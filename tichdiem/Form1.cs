using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace tichdiem
{
    public partial class Form1 : Form
    {
        // ==========================================================
        // MODEL
        // ==========================================================

        public class Customer
        {
            public string Phone { get; set; } = "";
            public string Name { get; set; } = "";
            public int Points { get; set; }
            public string Note { get; set; } = "";
        }

        public class BillingItem
        {
            public string Name { get; set; } = "";
            public decimal UnitPrice { get; set; }
            public int Qty { get; set; }

            public decimal LineTotal
            {
                get { return UnitPrice * Qty; }
            }
        }

        public class Coupon
        {
            public string Code { get; set; } = "";
            public string Purpose { get; set; } = "";
            public DateTime CreatedDate { get; set; }
            public DateTime ExpiryDate { get; set; }
        }

        public class Product
        {
            public string Name { get; set; } = "";
            public decimal UnitPrice { get; set; }

            // Đánh dấu sản phẩm này có thể dùng điểm để đổi hay không
            public bool IsReward { get; set; }

            // Số điểm cần để đổi lấy sản phẩm này (chỉ có ý nghĩa khi IsReward = true)
            public int RewardPoints { get; set; }

            public string Note { get; set; } = "";

            public override string ToString()
            {
                return Name;
            }
        }

        // Lịch sử mua hàng / cộng điểm của khách hàng
        public class Invoice
        {
            public string CustomerPhone { get; set; } = "";
            public DateTime Date { get; set; }

            // "Hóa đơn" (từ tab Tính Tiền) hoặc "Tích điểm nhanh" (từ tab Tích Điểm)
            public string Type { get; set; } = "Hóa đơn";

            public List<BillingItem> Items { get; set; } = new List<BillingItem>();

            public decimal OriginalTotal { get; set; }
            public decimal Discount { get; set; }
            public decimal FinalTotal { get; set; }

            public int PointsEarned { get; set; }
            public int PointsUsed { get; set; }
            public string RewardName { get; set; } = "";
        }

        // ==========================================================
        // DỮ LIỆU
        // ==========================================================

        private List<Customer> customerList = new List<Customer>();
        private List<BillingItem> billingCart = new List<BillingItem>();
        private List<Product> productList = new List<Product>();
        private List<Coupon> couponList = new List<Coupon>();
        private List<Invoice> invoiceList = new List<Invoice>();

        private Customer? selectedCustomer = null;

        // Số tiền (VNĐ) cần chi để được cộng 1 điểm
        private decimal pointsRatioAmount = 100000m;

        // 1 điểm quy đổi được bao nhiêu VNĐ khi dùng để giảm hóa đơn
        private decimal pointValueAmount = 1000m;

        private static readonly Random couponRandom = new Random();

        // ==========================================================
        // GIAO DIỆN CHÍNH
        // ==========================================================

        private Panel pnlTopBar = null!;
        private Panel pnlNavTabs = null!;
        private Panel pnlMainContent = null!;

        private Button[] navButtons = new Button[5];

        private Panel pnlViewPoints = null!;
        private Panel pnlViewBilling = null!;
        private Panel pnlViewProducts = null!;
        private Panel pnlViewSettings = null!;
        private Panel pnlViewInvoices = null!;

        // ==========================================================
        // TÍCH ĐIỂM (thao tác thủ công, độc lập với hóa đơn)
        // ==========================================================

        private FlowLayoutPanel flpCustomerCards = null!;

        private TextBox txtSearchInput = null!;
        private TextBox txtPhoneInput = null!;
        private TextBox txtNameInput = null!;

        private Label lblSelectedCustomerName = null!;
        private Label lblSelectedCustomerPoints = null!;

        private TextBox txtQuickAmountInput = null!;
        private Label lblQuickPreviewEarned = null!;

        private Button btnQuickCreditPoints = null!;
        private Button btnCreateCustomer = null!;

        private System.Windows.Forms.Timer searchDebounceTimer = null!;
        private ToolTip customerNoteToolTip = null!;

        // ==========================================================
        // TÍNH TIỀN
        // ==========================================================

        private ListView lvBillCart = null!;
        private Label lblBillTotal = null!;

        private TextBox txtBillItemName = null!;
        private TextBox txtBillItemPrice = null!;
        private NumericUpDown numBillItemQty = null!;

        private TextBox txtProductSearchBill = null!;
        private ComboBox cboSavedProducts = null!;
        private System.Windows.Forms.Timer productSearchDebounceTimer = null!;

        // ==========================================================
        // SẢN PHẨM
        // ==========================================================

        private TextBox txtProductName = null!;
        private TextBox txtProductPrice = null!;
        private CheckBox chkProductIsReward = null!;
        private NumericUpDown numProductRewardPoints = null!;
        private TextBox txtProductNote = null!;
        private ListView lvProducts = null!;

        // ==========================================================
        // CÀI ĐẶT
        // ==========================================================

        private NumericUpDown numPointsRatio = null!;
        private NumericUpDown numPointValue = null!;

        private NumericUpDown numCouponDays = null!;
        private TextBox txtCouponPurpose = null!;
        private ListView lvCoupons = null!;

        // ==========================================================
        // HÓA ĐƠN (xem tất cả hóa đơn của tất cả khách hàng)
        // ==========================================================

        private ListView lvAllInvoices = null!;
        private Label lblAllInvoicesSummary = null!;
        private RadioButton rbInvAll = null!, rbInvDay = null!, rbInvMonth = null!, rbInvYear = null!, rbInvRange = null!;
        private DateTimePicker dtpInvDay = null!, dtpInvFrom = null!, dtpInvTo = null!;
        private NumericUpDown numInvMonth = null!, numInvMonthYear = null!, numInvYear = null!;

        // ==========================================================
        // CONSTRUCTOR
        // ==========================================================

        public Form1()
        {
            InitializeComponent();

            this.DoubleBuffered = true;

            InitSampleData();

            SetupModernPOSUI();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        // ==========================================================
        // DỮ LIỆU MẪU
        // ==========================================================

        private void InitSampleData()
        {
            customerList.Add(new Customer { Phone = "0912345678", Name = "Nguyễn Văn A", Points = 35 });
            customerList.Add(new Customer { Phone = "0987654321", Name = "Trần Thị B", Points = 120 });
            customerList.Add(new Customer { Phone = "0933445566", Name = "Lê Hoàng C", Points = 15 });
            customerList.Add(new Customer { Phone = "0909112233", Name = "Phạm Minh Hùng", Points = 50 });
            customerList.Add(new Customer { Phone = "0977889900", Name = "Đỗ Hùng Dũng", Points = 80 });
            customerList.Add(new Customer { Phone = "0981122334", Name = "Vũ Thị Hùng", Points = 10 });

            // Sản phẩm mẫu. Giá chỉ dùng để TEST phần mềm, không phải giá bán thực tế.
            productList.Add(new Product { Name = "Paracetamol 500mg", UnitPrice = 1500, IsReward = false });
            productList.Add(new Product { Name = "Ibuprofen 200mg", UnitPrice = 2000, IsReward = false });
            productList.Add(new Product { Name = "Vitamin C 500mg", UnitPrice = 2500, IsReward = true, RewardPoints = 15 });
            productList.Add(new Product { Name = "Cetirizine 10mg", UnitPrice = 1200, IsReward = false });
            productList.Add(new Product { Name = "Oresol", UnitPrice = 5000, IsReward = false });
            productList.Add(new Product { Name = "Nước muối sinh lý 0.9%", UnitPrice = 8000, IsReward = true, RewardPoints = 30 });
            productList.Add(new Product { Name = "Siro ho", UnitPrice = 45000, IsReward = false });
            productList.Add(new Product { Name = "Dầu gió", UnitPrice = 25000, IsReward = true, RewardPoints = 80 });
            productList.Add(new Product { Name = "Băng cá nhân", UnitPrice = 15000, IsReward = false });
            productList.Add(new Product { Name = "Khẩu trang y tế", UnitPrice = 30000, IsReward = true, RewardPoints = 100 });
            productList.Add(new Product { Name = "Nhiệt kế điện tử", UnitPrice = 85000, IsReward = false });
            productList.Add(new Product { Name = "Vitamin B Complex", UnitPrice = 35000, IsReward = true, RewardPoints = 120 });
            productList.Add(new Product { Name = "Kẽm Zinc", UnitPrice = 40000, IsReward = false });
            productList.Add(new Product { Name = "Nước rửa tay", UnitPrice = 30000, IsReward = true, RewardPoints = 100 });
            productList.Add(new Product { Name = "Gạc y tế", UnitPrice = 12000, IsReward = false });
            productList.Add(new Product { Name = "Bông y tế", UnitPrice = 10000, IsReward = false });

            // Hóa đơn mẫu, trải ở nhiều mốc thời gian khác nhau,
            // để test các bộ lọc theo ngày / tháng / năm / khoảng ngày.
            invoiceList.Add(new Invoice
            {
                CustomerPhone = "0912345678",
                Date = DateTime.Now,
                Type = "Hóa đơn",
                Items = new List<BillingItem>
                {
                    new BillingItem { Name = "Paracetamol 500mg", UnitPrice = 1500, Qty = 10 },
                    new BillingItem { Name = "Oresol", UnitPrice = 5000, Qty = 2 }
                },
                OriginalTotal = 25000,
                Discount = 0,
                FinalTotal = 25000,
                PointsEarned = 0,
                PointsUsed = 0,
                RewardName = ""
            });

            invoiceList.Add(new Invoice
            {
                CustomerPhone = "0912345678",
                Date = DateTime.Now.AddDays(-10),
                Type = "Tích điểm nhanh",
                Items = new List<BillingItem>(),
                OriginalTotal = 300000,
                Discount = 0,
                FinalTotal = 300000,
                PointsEarned = 3,
                PointsUsed = 0,
                RewardName = ""
            });

            invoiceList.Add(new Invoice
            {
                CustomerPhone = "0987654321",
                Date = DateTime.Now.AddDays(-1),
                Type = "Hóa đơn",
                Items = new List<BillingItem>
                {
                    new BillingItem { Name = "Vitamin C 500mg", UnitPrice = 2500, Qty = 4 },
                    new BillingItem { Name = "Khẩu trang y tế", UnitPrice = 30000, Qty = 1 }
                },
                OriginalTotal = 40000,
                Discount = 5000,
                FinalTotal = 35000,
                PointsEarned = 0,
                PointsUsed = 5,
                RewardName = ""
            });

            invoiceList.Add(new Invoice
            {
                CustomerPhone = "0909112233",
                Date = DateTime.Now.AddMonths(-1),
                Type = "Hóa đơn",
                Items = new List<BillingItem>
                {
                    new BillingItem { Name = "Dầu gió", UnitPrice = 25000, Qty = 1 }
                },
                OriginalTotal = 25000,
                Discount = 0,
                FinalTotal = 0,
                PointsEarned = 0,
                PointsUsed = 80,
                RewardName = "Dầu gió"
            });

            invoiceList.Add(new Invoice
            {
                CustomerPhone = "0977889900",
                Date = DateTime.Now.AddDays(-45),
                Type = "Hóa đơn",
                Items = new List<BillingItem>
                {
                    new BillingItem { Name = "Siro ho", UnitPrice = 45000, Qty = 1 },
                    new BillingItem { Name = "Băng cá nhân", UnitPrice = 15000, Qty = 3 }
                },
                OriginalTotal = 90000,
                Discount = 0,
                FinalTotal = 90000,
                PointsEarned = 0,
                PointsUsed = 0,
                RewardName = ""
            });

            invoiceList.Add(new Invoice
            {
                CustomerPhone = "0981122334",
                Date = DateTime.Now.AddDays(-2),
                Type = "Tích điểm nhanh",
                Items = new List<BillingItem>(),
                OriginalTotal = 150000,
                Discount = 0,
                FinalTotal = 150000,
                PointsEarned = 1,
                PointsUsed = 0,
                RewardName = ""
            });
        }

        // ==========================================================
        // GIAO DIỆN CHÍNH
        // ==========================================================

        private void SetupModernPOSUI()
        {
            this.Controls.Clear();

            this.Text = "Hệ Thống Quản Lý Tích Điểm - Nhà Thuốc POS";
            this.Size = new Size(1120, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(241, 245, 249);
            this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);

            customerNoteToolTip = new ToolTip();

            pnlTopBar = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(15, 23, 42) };
            Label lblAppLogo = new Label
            {
                Text = "💊 NHÀ THUỐC POS",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true
            };
            pnlTopBar.Controls.Add(lblAppLogo);

            pnlNavTabs = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.FromArgb(30, 41, 59) };
            FlowLayoutPanel flpNav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(10, 3, 0, 0)
            };

            navButtons[0] = CreateNavButton("🎯 TÍCH ĐIỂM", 0);
            navButtons[1] = CreateNavButton("🧾 TÍNH TIỀN", 1);
            navButtons[2] = CreateNavButton("📦 SẢN PHẨM", 2);
            navButtons[3] = CreateNavButton("⚙️ CÀI ĐẶT", 3);
            navButtons[4] = CreateNavButton("📜 HÓA ĐƠN", 4);

            flpNav.Controls.Add(navButtons[0]);
            flpNav.Controls.Add(navButtons[1]);
            flpNav.Controls.Add(navButtons[2]);
            flpNav.Controls.Add(navButtons[3]);
            flpNav.Controls.Add(navButtons[4]);

            pnlNavTabs.Controls.Add(flpNav);

            pnlMainContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            this.Controls.Add(pnlMainContent);
            this.Controls.Add(pnlNavTabs);
            this.Controls.Add(pnlTopBar);

            pnlViewPoints = BuildPointsView();
            pnlViewBilling = BuildBillingView();
            pnlViewProducts = BuildProductsView();
            pnlViewSettings = BuildSettingsView();
            pnlViewInvoices = BuildInvoicesView();

            pnlViewPoints.Dock = DockStyle.Fill;
            pnlViewBilling.Dock = DockStyle.Fill;
            pnlViewProducts.Dock = DockStyle.Fill;
            pnlViewSettings.Dock = DockStyle.Fill;
            pnlViewInvoices.Dock = DockStyle.Fill;

            pnlMainContent.Controls.Add(pnlViewInvoices);
            pnlMainContent.Controls.Add(pnlViewSettings);
            pnlMainContent.Controls.Add(pnlViewProducts);
            pnlMainContent.Controls.Add(pnlViewBilling);
            pnlMainContent.Controls.Add(pnlViewPoints);

            ShowView(0);
        }

        private Button CreateNavButton(string text, int index)
        {
            Button btn = new Button
            {
                Text = text,
                Width = 160,
                Height = 40,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => { ShowView(index); };
            return btn;
        }

        private void ShowView(int index)
        {
            pnlViewPoints.Visible = index == 0;
            pnlViewBilling.Visible = index == 1;
            pnlViewProducts.Visible = index == 2;
            pnlViewSettings.Visible = index == 3;
            pnlViewInvoices.Visible = index == 4;

            for (int i = 0; i < navButtons.Length; i++)
            {
                navButtons[i].BackColor = i == index
                    ? Color.FromArgb(14, 165, 233)
                    : Color.FromArgb(30, 41, 59);
            }

            if (index == 2) RefreshProductList();
            if (index == 3) RefreshCouponList();
            if (index == 4) RefreshAllInvoicesList();
        }

        private string FormatVnd(decimal amount)
        {
            return amount.ToString("#,##0", CultureInfo.InvariantCulture) + " VNĐ";
        }

        // ==========================================================
        // TAB TÍCH ĐIỂM (thao tác thủ công, độc lập với hóa đơn)
        // ==========================================================

        private Panel BuildPointsView()
        {
            Panel viewContainer = new Panel { BackColor = Color.Transparent };

            Panel pnlPaymentBox = CreatePaymentPanel();
            pnlPaymentBox.Dock = DockStyle.Left;
            pnlPaymentBox.Width = 380;

            Panel pnlRightSection = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 0, 0, 0),
                BackColor = Color.Transparent
            };

            Panel pnlHeaderContainer = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.Transparent };
            Label lblGridHeader = new Label
            {
                Text = "DANH SÁCH KHÁCH HÀNG (Lọc tự động theo SĐT / Tên — bấm vào khách để xem lịch sử mua hàng)",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(0, 5),
                AutoSize = true
            };
            pnlHeaderContainer.Controls.Add(lblGridHeader);

            flpCustomerCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(0, 10, 0, 10),
                BackColor = Color.Transparent
            };

            pnlRightSection.Controls.Add(flpCustomerCards);
            pnlRightSection.Controls.Add(pnlHeaderContainer);
            flpCustomerCards.BringToFront();

            viewContainer.Controls.Add(pnlRightSection);
            viewContainer.Controls.Add(pnlPaymentBox);
            pnlRightSection.BringToFront();

            ExecuteInstantFilterAndRedraw();

            return viewContainer;
        }

        private Panel CreatePaymentPanel()
        {
            Panel card = new Panel { BackColor = Color.White, Padding = new Padding(20) };

            Label lblSearchTitle = new Label
            {
                Text = "🔍 TÌM KIẾM NHANH (SĐT hoặc Tên):",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            card.Controls.Add(lblSearchTitle);

            txtSearchInput = new TextBox { Location = new Point(15, 40), Width = 330, Font = new Font("Segoe UI", 11F) };

            searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 200 };
            searchDebounceTimer.Tick += (s, e) => { searchDebounceTimer.Stop(); ExecuteInstantFilterAndRedraw(); };
            txtSearchInput.TextChanged += (s, e) => { searchDebounceTimer.Stop(); searchDebounceTimer.Start(); };

            card.Controls.Add(txtSearchInput);

            Panel pnlDivider = new Panel { Location = new Point(15, 80), Width = 330, Height = 1, BackColor = Color.FromArgb(226, 232, 240) };
            card.Controls.Add(pnlDivider);

            Label lblCustomerInfoTitle = new Label
            {
                Text = "THÔNG TIN KHÁCH HÀNG",
                Location = new Point(15, 95),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            card.Controls.Add(lblCustomerInfoTitle);

            Label lblPhone = new Label { Text = "Số điện thoại:", Location = new Point(15, 125), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            card.Controls.Add(lblPhone);

            txtPhoneInput = new TextBox { Location = new Point(15, 148), Width = 210, Font = new Font("Segoe UI", 10.5F) };
            card.Controls.Add(txtPhoneInput);

            btnCreateCustomer = new Button
            {
                Text = "+ Tạo Mới",
                Location = new Point(235, 147),
                Width = 110,
                Height = 30,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCreateCustomer.FlatAppearance.BorderSize = 0;
            btnCreateCustomer.Click += BtnCreateCustomer_Click;
            card.Controls.Add(btnCreateCustomer);

            Label lblName = new Label { Text = "Họ tên khách hàng:", Location = new Point(15, 185), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            card.Controls.Add(lblName);

            txtNameInput = new TextBox { Location = new Point(15, 208), Width = 330, Font = new Font("Segoe UI", 10.5F) };
            card.Controls.Add(txtNameInput);

            Panel pnlSelected = new Panel
            {
                Location = new Point(15, 250),
                Width = 330,
                Height = 75,
                BackColor = Color.FromArgb(240, 253, 244),
                BorderStyle = BorderStyle.FixedSingle
            };
            card.Controls.Add(pnlSelected);

            lblSelectedCustomerName = new Label
            {
                Text = "Chưa chọn khách hàng",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61),
                Location = new Point(10, 10),
                AutoSize = true
            };
            pnlSelected.Controls.Add(lblSelectedCustomerName);

            lblSelectedCustomerPoints = new Label
            {
                Text = "Điểm tích lũy: 0",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(22, 101, 52),
                Location = new Point(10, 38),
                AutoSize = true
            };
            pnlSelected.Controls.Add(lblSelectedCustomerPoints);

            Label lblQuickTitle = new Label
            {
                Text = "TÍCH ĐIỂM NHANH (nhập số tiền thủ công)",
                Location = new Point(15, 340),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            card.Controls.Add(lblQuickTitle);

            txtQuickAmountInput = new TextBox { Location = new Point(15, 363), Width = 330, Font = new Font("Segoe UI", 12F) };
            txtQuickAmountInput.TextChanged += TxtQuickAmountInput_TextChanged;
            card.Controls.Add(txtQuickAmountInput);

            lblQuickPreviewEarned = new Label
            {
                Text = "Sẽ cộng: +0 điểm",
                Location = new Point(15, 401),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            card.Controls.Add(lblQuickPreviewEarned);

            btnQuickCreditPoints = new Button
            {
                Text = "CỘNG ĐIỂM CHO KHÁCH",
                Location = new Point(15, 438),
                Width = 330,
                Height = 48,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnQuickCreditPoints.FlatAppearance.BorderSize = 0;
            btnQuickCreditPoints.Click += BtnQuickCreditPoints_Click;
            card.Controls.Add(btnQuickCreditPoints);

            return card;
        }

        private void ExecuteInstantFilterAndRedraw()
        {
            if (flpCustomerCards == null) return;

            string key = txtSearchInput.Text.Trim().ToLower();
            string cleanKey = RemoveVietnameseAccents(key);

            flpCustomerCards.SuspendLayout();
            flpCustomerCards.Controls.Clear();

            var filteredList = string.IsNullOrEmpty(key)
                ? customerList
                : customerList.Where(c =>
                    c.Phone.ToLower().Contains(key) ||
                    c.Name.ToLower().Contains(key) ||
                    RemoveVietnameseAccents(c.Name).Contains(cleanKey)).ToList();

            foreach (var cust in filteredList) AddCardToPanel(cust);

            flpCustomerCards.ResumeLayout(true);
        }

        private void AddCardToPanel(Customer cust)
        {
            Panel card = new Panel
            {
                Width = 200,
                Height = 110,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 15, 15),
                Cursor = Cursors.Hand,
                Tag = cust
            };

            Panel topBorder = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = Color.FromArgb(14, 165, 233) };
            card.Controls.Add(topBorder);

            Label lblName = new Label
            {
                Text = cust.Name,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(12, 12),
                AutoSize = true
            };
            card.Controls.Add(lblName);

            Label lblPhone = new Label
            {
                Text = "📱 " + cust.Phone,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(12, 40),
                AutoSize = true
            };
            card.Controls.Add(lblPhone);

            Label lblBadge = new Label
            {
                Name = "lblPointsBadge",
                Text = $"{cust.Points} Điểm",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(34, 197, 94),
                Location = new Point(12, 70),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true
            };
            card.Controls.Add(lblBadge);

            // Icon ghi chú: chỉ hiện nếu khách hàng có note, hover để xem nội dung
            if (!string.IsNullOrEmpty(cust.Note))
            {
                Label lblNoteIcon = new Label
                {
                    Text = "📝",
                    Location = new Point(168, 10),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10F),
                    Cursor = Cursors.Hand
                };
                customerNoteToolTip.SetToolTip(lblNoteIcon, cust.Note);
                card.Controls.Add(lblNoteIcon);
            }

            // Bấm vào khách: chọn khách (đổ dữ liệu vào ô bên trái)
            // đồng thời mở lịch sử mua hàng của khách đó.
            Action selectAction = () =>
            {
                SelectCustomer(cust);
                ShowPurchaseHistoryDialog(cust);
            };

            card.Click += (s, e) => { selectAction(); };
            lblName.Click += (s, e) => { selectAction(); };
            lblPhone.Click += (s, e) => { selectAction(); };
            lblBadge.Click += (s, e) => { selectAction(); };

            // Chuột phải vào thẻ khách hàng: Sửa / Xóa
            ContextMenuStrip cmCustomer = new ContextMenuStrip();

            ToolStripMenuItem miEditCustomer = new ToolStripMenuItem("✏️ Sửa thông tin khách hàng");
            miEditCustomer.Click += (s, e) => { ShowEditCustomerDialog(cust); };
            cmCustomer.Items.Add(miEditCustomer);

            ToolStripMenuItem miDeleteCustomer = new ToolStripMenuItem("🗑 Xóa khách hàng");
            miDeleteCustomer.Click += (s, e) => { DeleteCustomerWithConfirm(cust); };
            cmCustomer.Items.Add(miDeleteCustomer);

            card.ContextMenuStrip = cmCustomer;
            lblName.ContextMenuStrip = cmCustomer;
            lblPhone.ContextMenuStrip = cmCustomer;
            lblBadge.ContextMenuStrip = cmCustomer;

            flpCustomerCards.Controls.Add(card);
        }

        private string RemoveVietnameseAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd').Replace('Đ', 'D').ToLower();
        }

        // Hàm này CHỈ cập nhật state + UI của khách hàng đang chọn.
        private void SelectCustomer(Customer cust)
        {
            selectedCustomer = cust;

            if (txtPhoneInput != null) txtPhoneInput.Text = cust.Phone;
            if (txtNameInput != null) txtNameInput.Text = cust.Name;

            if (lblSelectedCustomerName != null) lblSelectedCustomerName.Text = cust.Name;
            if (lblSelectedCustomerPoints != null) lblSelectedCustomerPoints.Text = $"Điểm tích lũy: {cust.Points}";
        }

        // ==========================================================
        // SỬA / XÓA THÔNG TIN KHÁCH HÀNG (chuột phải vào thẻ khách hàng)
        // ==========================================================

        private void ShowEditCustomerDialog(Customer cust)
        {
            Form dlg = new Form
            {
                Text = "Sửa thông tin khách hàng",
                Size = new Size(420, 360),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblTitle = new Label
            {
                Text = "✏️ SỬA THÔNG TIN KHÁCH HÀNG",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            dlg.Controls.Add(lblTitle);

            Label lblPhone = new Label { Text = "Số điện thoại:", Location = new Point(20, 55), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblPhone);

            TextBox txtPhone = new TextBox { Location = new Point(20, 78), Width = 360, Text = cust.Phone, Font = new Font("Segoe UI", 10.5F) };
            dlg.Controls.Add(txtPhone);

            Label lblName = new Label { Text = "Họ tên:", Location = new Point(20, 115), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblName);

            TextBox txtName = new TextBox { Location = new Point(20, 138), Width = 360, Text = cust.Name, Font = new Font("Segoe UI", 10.5F) };
            dlg.Controls.Add(txtName);

            Label lblNote = new Label { Text = "Ghi chú:", Location = new Point(20, 175), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblNote);

            TextBox txtNote = new TextBox
            {
                Location = new Point(20, 198),
                Width = 360,
                Height = 70,
                Multiline = true,
                Text = cust.Note,
                Font = new Font("Segoe UI", 10F)
            };
            dlg.Controls.Add(txtNote);

            Button btnSave = new Button
            {
                Text = "💾 LƯU",
                Location = new Point(195, 285),
                Width = 185,
                Height = 40,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnSave);

            Button btnCancel = new Button
            {
                Text = "HỦY",
                Location = new Point(20, 285),
                Width = 165,
                Height = 40,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnSave;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string newPhone = txtPhone.Text.Trim();
            string newName = txtName.Text.Trim();
            string newNote = txtNote.Text.Trim();

            if (string.IsNullOrEmpty(newPhone) || string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Số điện thoại và Họ tên!", "Cảnh báo");
                return;
            }

            bool phoneUsedByOther = customerList.Any(c => c != cust && c.Phone == newPhone);
            if (phoneUsedByOther)
            {
                MessageBox.Show("Số điện thoại này đã được dùng bởi khách hàng khác!", "Cảnh báo");
                return;
            }

            string oldPhone = cust.Phone;

            cust.Phone = newPhone;
            cust.Name = newName;
            cust.Note = newNote;

            if (oldPhone != newPhone)
            {
                foreach (Invoice inv in invoiceList.Where(i => i.CustomerPhone == oldPhone))
                    inv.CustomerPhone = newPhone;
            }

            ExecuteInstantFilterAndRedraw();

            if (selectedCustomer == cust)
                SelectCustomer(cust);

            MessageBox.Show("Đã cập nhật thông tin khách hàng!", "Thành công");
        }

        private void DeleteCustomerWithConfirm(Customer cust)
        {
            DialogResult result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa khách hàng '{cust.Name}' ({cust.Phone}) không?\nHành động này không thể hoàn tác.",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            customerList.Remove(cust);

            if (selectedCustomer == cust)
            {
                selectedCustomer = null;
                if (txtPhoneInput != null) txtPhoneInput.Clear();
                if (txtNameInput != null) txtNameInput.Clear();
                if (lblSelectedCustomerName != null) lblSelectedCustomerName.Text = "Chưa chọn khách hàng";
                if (lblSelectedCustomerPoints != null) lblSelectedCustomerPoints.Text = "Điểm tích lũy: 0";
            }

            ExecuteInstantFilterAndRedraw();

            MessageBox.Show("Đã xóa khách hàng!", "Thành công");
        }

        private void BtnCreateCustomer_Click(object? sender, EventArgs e)
        {
            string phone = txtPhoneInput.Text.Trim();
            string name = txtNameInput.Text.Trim();

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Số điện thoại và Họ tên!", "Cảnh báo");
                return;
            }

            var existing = customerList.FirstOrDefault(c => c.Phone == phone);
            if (existing != null)
            {
                SelectCustomer(existing);
                MessageBox.Show($"Số điện thoại này đã tồn tại! Đã chọn khách hàng: {existing.Name}", "Thông báo");
                return;
            }

            Customer newCust = new Customer { Phone = phone, Name = name, Points = 0 };
            customerList.Add(newCust);

            ExecuteInstantFilterAndRedraw();
            SelectCustomer(newCust);

            MessageBox.Show($"Đã tạo mới khách hàng: {name}", "Thành công");
        }

        private void TxtQuickAmountInput_TextChanged(object? sender, EventArgs e)
        {
            if (decimal.TryParse(txtQuickAmountInput.Text, out decimal amount) && amount > 0)
            {
                int earned = (int)(amount / pointsRatioAmount);
                lblQuickPreviewEarned.Text = $"Sẽ cộng: +{earned} điểm ({FormatVnd(pointsRatioAmount)} = 1 điểm)";
            }
            else
            {
                lblQuickPreviewEarned.Text = "Sẽ cộng: +0 điểm";
            }
        }

        private void BtnQuickCreditPoints_Click(object? sender, EventArgs e)
        {
            if (selectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn hoặc tạo khách hàng trước!", "Cảnh báo");
                return;
            }

            if (!decimal.TryParse(txtQuickAmountInput.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền hợp lệ!", "Cảnh báo");
                return;
            }

            int earned = (int)(amount / pointsRatioAmount);
            selectedCustomer.Points += earned;

            invoiceList.Add(new Invoice
            {
                CustomerPhone = selectedCustomer.Phone,
                Date = DateTime.Now,
                Type = "Tích điểm nhanh",
                Items = new List<BillingItem>(),
                OriginalTotal = amount,
                Discount = 0,
                FinalTotal = amount,
                PointsEarned = earned,
                PointsUsed = 0,
                RewardName = ""
            });

            ExecuteInstantFilterAndRedraw();
            SelectCustomer(selectedCustomer);

            MessageBox.Show(
                $"Đã cộng điểm!\n\nSố tiền: {FormatVnd(amount)}\nCộng thêm: +{earned} điểm\nTổng điểm hiện tại: {selectedCustomer.Points}",
                "Thành công");

            txtQuickAmountInput.Clear();
        }

        // ==========================================================
        // LỊCH SỬ MUA HÀNG THEO KHÁCH HÀNG
        // ==========================================================

        private void ShowPurchaseHistoryDialog(Customer cust)
        {
            Form dlg = new Form
            {
                Text = $"Lịch sử mua hàng - {cust.Name}",
                Size = new Size(780, 640),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblHeader = new Label
            {
                Text = $"🧾 {cust.Name}   |   📱 {cust.Phone}   |   ⭐ {cust.Points} điểm hiện có",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            dlg.Controls.Add(lblHeader);

            RadioButton rbAll = new RadioButton
            {
                Text = "Tất cả (mặc định — mới nhất trước)",
                Location = new Point(20, 55),
                AutoSize = true,
                Checked = true
            };
            dlg.Controls.Add(rbAll);

            RadioButton rbDay = new RadioButton { Text = "Theo ngày:", Location = new Point(20, 85), AutoSize = true };
            dlg.Controls.Add(rbDay);

            DateTimePicker dtpDay = new DateTimePicker
            {
                Location = new Point(150, 82),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            dlg.Controls.Add(dtpDay);

            RadioButton rbMonth = new RadioButton { Text = "Theo tháng:", Location = new Point(20, 118), AutoSize = true };
            dlg.Controls.Add(rbMonth);

            NumericUpDown numMonth = new NumericUpDown
            {
                Location = new Point(150, 115),
                Width = 60,
                Minimum = 1,
                Maximum = 12,
                Value = DateTime.Now.Month
            };
            dlg.Controls.Add(numMonth);

            Label lblSlash = new Label { Text = "/", Location = new Point(215, 118), AutoSize = true };
            dlg.Controls.Add(lblSlash);

            NumericUpDown numMonthYear = new NumericUpDown
            {
                Location = new Point(232, 115),
                Width = 80,
                Minimum = 2000,
                Maximum = 2100,
                Value = DateTime.Now.Year
            };
            dlg.Controls.Add(numMonthYear);

            RadioButton rbYear = new RadioButton { Text = "Theo năm:", Location = new Point(20, 151), AutoSize = true };
            dlg.Controls.Add(rbYear);

            NumericUpDown numYear = new NumericUpDown
            {
                Location = new Point(150, 148),
                Width = 90,
                Minimum = 2000,
                Maximum = 2100,
                Value = DateTime.Now.Year
            };
            dlg.Controls.Add(numYear);

            RadioButton rbRange = new RadioButton { Text = "Khoảng ngày:", Location = new Point(20, 184), AutoSize = true };
            dlg.Controls.Add(rbRange);

            DateTimePicker dtpFrom = new DateTimePicker
            {
                Location = new Point(150, 181),
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddDays(-30)
            };
            dlg.Controls.Add(dtpFrom);

            Label lblTo = new Label { Text = "đến", Location = new Point(298, 184), AutoSize = true };
            dlg.Controls.Add(lblTo);

            DateTimePicker dtpTo = new DateTimePicker
            {
                Location = new Point(330, 181),
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            dlg.Controls.Add(dtpTo);

            Button btnApplyFilter = new Button
            {
                Text = "🔍 LỌC",
                Location = new Point(510, 82),
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnApplyFilter.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnApplyFilter);

            Button btnResetFilter = new Button
            {
                Text = "Xem Tất Cả",
                Location = new Point(510, 122),
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResetFilter.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnResetFilter);

            Panel pnlDivider = new Panel { Location = new Point(20, 220), Width = 720, Height = 1, BackColor = Color.FromArgb(226, 232, 240) };
            dlg.Controls.Add(pnlDivider);

            ListView lvHistory = new ListView
            {
                Location = new Point(20, 232),
                Width = 720,
                Height = 300,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            lvHistory.Columns.Add("Ngày giờ", 110);
            lvHistory.Columns.Add("Loại", 100);
            lvHistory.Columns.Add("Sản phẩm", 190);
            lvHistory.Columns.Add("Tổng tiền", 90);
            lvHistory.Columns.Add("Giảm giá", 80);
            lvHistory.Columns.Add("Đổi quà", 90);
            lvHistory.Columns.Add("Dùng điểm", 60);
            dlg.Controls.Add(lvHistory);

            Label lblSummary = new Label
            {
                Text = "",
                Location = new Point(20, 542),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61)
            };
            dlg.Controls.Add(lblSummary);

            void RefreshHistory()
            {
                List<Invoice> source = invoiceList
                    .Where(i => i.CustomerPhone == cust.Phone)
                    .ToList();

                List<Invoice> filtered;

                if (rbDay.Checked)
                {
                    DateTime day = dtpDay.Value.Date;
                    filtered = source.Where(i => i.Date.Date == day).ToList();
                }
                else if (rbMonth.Checked)
                {
                    int month = (int)numMonth.Value;
                    int year = (int)numMonthYear.Value;
                    filtered = source.Where(i => i.Date.Month == month && i.Date.Year == year).ToList();
                }
                else if (rbYear.Checked)
                {
                    int year = (int)numYear.Value;
                    filtered = source.Where(i => i.Date.Year == year).ToList();
                }
                else if (rbRange.Checked)
                {
                    DateTime from = dtpFrom.Value.Date;
                    DateTime to = dtpTo.Value.Date;
                    filtered = source.Where(i => i.Date.Date >= from && i.Date.Date <= to).ToList();
                }
                else
                {
                    filtered = source;
                }

                filtered = filtered.OrderByDescending(i => i.Date).ToList();

                lvHistory.Items.Clear();
                decimal sumFinal = 0;

                foreach (Invoice inv in filtered)
                {
                    string itemsText = inv.Items.Count > 0
                        ? string.Join(", ", inv.Items.Select(x => $"{x.Name} x{x.Qty}"))
                        : "-";

                    ListViewItem lvi = new ListViewItem(inv.Date.ToString("dd/MM/yyyy HH:mm"));
                    lvi.SubItems.Add(inv.Type);
                    lvi.SubItems.Add(itemsText);
                    lvi.SubItems.Add(FormatVnd(inv.FinalTotal));
                    lvi.SubItems.Add(inv.Discount > 0 ? FormatVnd(inv.Discount) : "-");
                    lvi.SubItems.Add(string.IsNullOrEmpty(inv.RewardName) ? "-" : inv.RewardName);
                    lvi.SubItems.Add(inv.PointsUsed > 0 ? inv.PointsUsed.ToString() : "-");
                    lvHistory.Items.Add(lvi);

                    sumFinal += inv.FinalTotal;
                }

                lblSummary.Text = $"Tổng số hóa đơn: {filtered.Count}    |    Tổng tiền: {FormatVnd(sumFinal)}";
            }

            btnApplyFilter.Click += (s, e) => { RefreshHistory(); };
            btnResetFilter.Click += (s, e) => { rbAll.Checked = true; RefreshHistory(); };

            RefreshHistory();

            Button btnClose = new Button
            {
                Text = "ĐÓNG",
                Location = new Point(640, 542),
                Width = 100,
                Height = 36,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnClose);

            dlg.AcceptButton = btnClose;
            dlg.CancelButton = btnClose;

            dlg.ShowDialog(this);
        }

        // ==========================================================
        // TAB TÍNH TIỀN
        // ==========================================================

        private Panel BuildBillingView()
        {
            Panel viewContainer = new Panel { BackColor = Color.Transparent };

            Panel pnlBillLeft = new Panel
            {
                Dock = DockStyle.Left,
                Width = 380,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Label lblTitle = new Label
            {
                Text = "🧾 TÍNH TIỀN",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            pnlBillLeft.Controls.Add(lblTitle);

            Label lblSearchProduct = new Label
            {
                Text = "🔍 Tìm nhanh thuốc (gõ tên để lọc danh sách bên dưới):",
                Location = new Point(15, 50),
                Width = 340,
                AutoSize = false,
                Height = 16,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F)
            };
            pnlBillLeft.Controls.Add(lblSearchProduct);

            txtProductSearchBill = new TextBox
            {
                Location = new Point(15, 70),
                Width = 330,
                Font = new Font("Segoe UI", 10.5F)
            };

            productSearchDebounceTimer = new System.Windows.Forms.Timer { Interval = 200 };
            productSearchDebounceTimer.Tick += (s, e) =>
            {
                productSearchDebounceTimer.Stop();
                RefreshSavedProductsCombo(txtProductSearchBill.Text);
            };
            txtProductSearchBill.TextChanged += (s, e) =>
            {
                productSearchDebounceTimer.Stop();
                productSearchDebounceTimer.Start();
            };

            pnlBillLeft.Controls.Add(txtProductSearchBill);

            Label lblSavedProduct = new Label
            {
                Text = "Sản phẩm đã lưu (theo kết quả tìm ở trên):",
                Location = new Point(15, 105),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlBillLeft.Controls.Add(lblSavedProduct);

            cboSavedProducts = new ComboBox
            {
                Location = new Point(15, 128),
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5F)
            };
            cboSavedProducts.SelectedIndexChanged += CboSavedProducts_SelectedIndexChanged;
            pnlBillLeft.Controls.Add(cboSavedProducts);

            Label lblUseHint = new Label
            {
                Text = "Chọn sản phẩm để tự điền tên + đơn giá, hoặc nhập thủ công bên dưới.",
                Location = new Point(15, 162),
                Width = 330,
                Height = 32,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };
            pnlBillLeft.Controls.Add(lblUseHint);

            Label lblItemName = new Label { Text = "Tên sản phẩm:", Location = new Point(15, 200), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            pnlBillLeft.Controls.Add(lblItemName);

            txtBillItemName = new TextBox { Location = new Point(15, 223), Width = 330, Font = new Font("Segoe UI", 10.5F) };
            pnlBillLeft.Controls.Add(txtBillItemName);

            Label lblItemPrice = new Label { Text = "Đơn giá (VNĐ):", Location = new Point(15, 260), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            pnlBillLeft.Controls.Add(lblItemPrice);

            txtBillItemPrice = new TextBox { Location = new Point(15, 283), Width = 160, Font = new Font("Segoe UI", 10.5F) };
            pnlBillLeft.Controls.Add(txtBillItemPrice);

            Label lblItemQty = new Label { Text = "Số lượng:", Location = new Point(190, 260), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            pnlBillLeft.Controls.Add(lblItemQty);

            numBillItemQty = new NumericUpDown { Location = new Point(190, 283), Width = 155, Minimum = 1, Maximum = 9999, Value = 1, Font = new Font("Segoe UI", 10.5F) };
            pnlBillLeft.Controls.Add(numBillItemQty);

            Button btnAddBillItem = new Button
            {
                Text = "+ Thêm Vào Hóa Đơn",
                Location = new Point(15, 323),
                Width = 330,
                Height = 38,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddBillItem.FlatAppearance.BorderSize = 0;
            btnAddBillItem.Click += BtnAddBillItem_Click;
            pnlBillLeft.Controls.Add(btnAddBillItem);

            Button btnRemoveBillItem = new Button
            {
                Text = "🗑 Xóa Dòng Đã Chọn",
                Location = new Point(15, 369),
                Width = 330,
                Height = 34,
                BackColor = Color.FromArgb(248, 113, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRemoveBillItem.FlatAppearance.BorderSize = 0;
            btnRemoveBillItem.Click += BtnRemoveBillItem_Click;
            pnlBillLeft.Controls.Add(btnRemoveBillItem);

            Panel pnlTotalBox = new Panel { Location = new Point(15, 415), Width = 330, Height = 60, BackColor = Color.FromArgb(240, 253, 244), BorderStyle = BorderStyle.FixedSingle };
            pnlBillLeft.Controls.Add(pnlTotalBox);

            lblBillTotal = new Label
            {
                Text = "Tổng cộng: 0 VNĐ",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61),
                Location = new Point(10, 15),
                AutoSize = true
            };
            pnlTotalBox.Controls.Add(lblBillTotal);

            Button btnBillCheckout = new Button
            {
                Text = "THANH TOÁN HÓA ĐƠN",
                Location = new Point(15, 490),
                Width = 330,
                Height = 46,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBillCheckout.FlatAppearance.BorderSize = 0;
            btnBillCheckout.Click += BtnBillCheckout_Click;
            pnlBillLeft.Controls.Add(btnBillCheckout);

            Panel pnlBillRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 0, 0), BackColor = Color.Transparent };

            Label lblCartHeader = new Label
            {
                Text = "DANH SÁCH SẢN PHẨM TRONG HÓA ĐƠN",
                Dock = DockStyle.Top,
                Height = 35,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lvBillCart = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 10F)
            };
            lvBillCart.Columns.Add("Tên sản phẩm", 250);
            lvBillCart.Columns.Add("Đơn giá", 130);
            lvBillCart.Columns.Add("SL", 60);
            lvBillCart.Columns.Add("Thành tiền", 150);

            pnlBillRight.Controls.Add(lvBillCart);
            pnlBillRight.Controls.Add(lblCartHeader);
            lvBillCart.BringToFront();

            viewContainer.Controls.Add(pnlBillRight);
            viewContainer.Controls.Add(pnlBillLeft);
            pnlBillRight.BringToFront();

            RefreshSavedProductsCombo();

            return viewContainer;
        }

        private void CboSavedProducts_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cboSavedProducts.SelectedIndex <= 0) return;

            Product? product = cboSavedProducts.SelectedItem as Product;
            if (product == null) return;

            txtBillItemName.Text = product.Name;
            txtBillItemPrice.Text = product.UnitPrice.ToString("0", CultureInfo.InvariantCulture);
            numBillItemQty.Value = 1;
        }

        private void RefreshSavedProductsCombo(string keyword = "")
        {
            if (cboSavedProducts == null) return;

            cboSavedProducts.Items.Clear();
            cboSavedProducts.Items.Add(new Product { Name = "— Nhập sản phẩm thủ công —", UnitPrice = 0 });

            string key = keyword.Trim().ToLower();
            string cleanKey = RemoveVietnameseAccents(key);

            IEnumerable<Product> list = productList.OrderBy(p => p.Name);

            if (!string.IsNullOrEmpty(key))
            {
                list = list.Where(p =>
                    p.Name.ToLower().Contains(key) ||
                    RemoveVietnameseAccents(p.Name).Contains(cleanKey));
            }

            foreach (Product product in list)
                cboSavedProducts.Items.Add(product);

            cboSavedProducts.DisplayMember = "Name";
            cboSavedProducts.SelectedIndex = 0;
        }

        private void BtnAddBillItem_Click(object? sender, EventArgs e)
        {
            string name = txtBillItemName.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Vui lòng nhập tên sản phẩm hoặc chọn sản phẩm đã lưu!", "Cảnh báo");
                return;
            }

            if (!decimal.TryParse(txtBillItemPrice.Text, out decimal price) || price <= 0)
            {
                MessageBox.Show("Vui lòng nhập đơn giá hợp lệ!", "Cảnh báo");
                return;
            }

            int qty = (int)numBillItemQty.Value;

            BillingItem? existing = billingCart.FirstOrDefault(i =>
                string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase) && i.UnitPrice == price);

            if (existing != null)
                existing.Qty += qty;
            else
                billingCart.Add(new BillingItem { Name = name, UnitPrice = price, Qty = qty });

            RefreshBillCart();

            txtBillItemName.Clear();
            txtBillItemPrice.Clear();
            numBillItemQty.Value = 1;
            cboSavedProducts.SelectedIndex = 0;
            txtBillItemName.Focus();
        }

        private void BtnRemoveBillItem_Click(object? sender, EventArgs e)
        {
            if (lvBillCart.SelectedIndices.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn dòng cần xóa!", "Cảnh báo");
                return;
            }

            int idx = lvBillCart.SelectedIndices[0];
            if (idx < 0 || idx >= billingCart.Count) return;

            billingCart.RemoveAt(idx);
            RefreshBillCart();
        }

        private void RefreshBillCart()
        {
            if (lvBillCart == null) return;

            lvBillCart.Items.Clear();
            decimal total = 0;

            foreach (BillingItem item in billingCart)
            {
                ListViewItem lvi = new ListViewItem(item.Name);
                lvi.SubItems.Add(FormatVnd(item.UnitPrice));
                lvi.SubItems.Add(item.Qty.ToString());
                lvi.SubItems.Add(FormatVnd(item.LineTotal));
                lvBillCart.Items.Add(lvi);
                total += item.LineTotal;
            }

            if (lblBillTotal != null)
                lblBillTotal.Text = $"Tổng cộng: {FormatVnd(total)}";
        }

        private void BtnBillCheckout_Click(object? sender, EventArgs e)
        {
            if (billingCart.Count == 0)
            {
                MessageBox.Show("Hóa đơn đang trống!", "Cảnh báo");
                return;
            }

            decimal originalTotal = billingCart.Sum(i => i.LineTotal);
            decimal finalTotal = originalTotal;

            int usedPoints = 0;
            decimal discount = 0;
            Product? reward = null;
            Customer? billCustomer = null;

            DialogResult wantPoints = MessageBox.Show(
                $"Tổng tiền: {FormatVnd(originalTotal)}\n\nBạn có muốn gắn khách hàng vào hóa đơn này để tích/dùng điểm không?",
                "Tích điểm / Dùng điểm?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (wantPoints == DialogResult.Yes)
            {
                billCustomer = ChooseCustomerForCheckout();

                if (billCustomer == null)
                {
                    MessageBox.Show("Chưa chọn khách hàng. Hóa đơn chưa được thanh toán.", "Thông báo");
                    return;
                }

                if (billCustomer.Points > 0)
                {
                    DialogResult usePoints = MessageBox.Show(
                        $"Khách hàng: {billCustomer.Name}\nĐiểm hiện có: {billCustomer.Points}\n\nBạn có muốn dùng điểm cho hóa đơn này không?",
                        "Dùng điểm tích lũy?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (usePoints == DialogResult.Yes)
                    {
                        if (!ShowRedeemDialog(billCustomer, originalTotal, out usedPoints, out discount, out reward))
                        {
                            usedPoints = 0;
                            discount = 0;
                            reward = null;
                        }
                    }
                }
            }

            if (reward != null)
            {
                billCustomer!.Points -= reward.RewardPoints;
            }
            else if (usedPoints > 0)
            {
                billCustomer!.Points -= usedPoints;
                finalTotal = Math.Max(0, originalTotal - discount);
            }

            int earned = 0;
            if (billCustomer != null)
            {
                earned = (int)(finalTotal / pointsRatioAmount);
                billCustomer.Points += earned;

                invoiceList.Add(new Invoice
                {
                    CustomerPhone = billCustomer.Phone,
                    Date = DateTime.Now,
                    Type = "Hóa đơn",
                    Items = billingCart.Select(i => new BillingItem { Name = i.Name, UnitPrice = i.UnitPrice, Qty = i.Qty }).ToList(),
                    OriginalTotal = originalTotal,
                    Discount = discount,
                    FinalTotal = finalTotal,
                    PointsEarned = earned,
                    PointsUsed = usedPoints,
                    RewardName = reward != null ? reward.Name : ""
                });

                ExecuteInstantFilterAndRedraw();
                SelectCustomer(billCustomer);
            }

            string result = $"Thanh toán thành công!\nTổng trước giảm: {FormatVnd(originalTotal)}";

            if (reward != null)
                result += $"\n🎁 Đổi quà: {reward.Name} (-{reward.RewardPoints} điểm)";

            if (discount > 0)
                result += $"\n💰 Giảm do dùng điểm: -{FormatVnd(discount)}";

            result += $"\nThanh toán: {FormatVnd(finalTotal)}";

            if (billCustomer != null)
                result += $"\nCộng điểm: +{earned}\nĐiểm còn lại: {billCustomer.Points}";

            MessageBox.Show(result, "Hoàn tất");

            billingCart.Clear();
            RefreshBillCart();
        }

        private Customer? ChooseCustomerForCheckout()
        {
            Form dlg = new Form
            {
                Text = "Chọn khách hàng",
                Size = new Size(480, 500),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblHint = new Label
            {
                Text = "Nhập SĐT hoặc tên để tìm, rồi chọn khách hàng:",
                Location = new Point(20, 15),
                AutoSize = true
            };
            dlg.Controls.Add(lblHint);

            TextBox search = new TextBox
            {
                Location = new Point(20, 40),
                Width = 420,
                Font = new Font("Segoe UI", 10.5F)
            };
            dlg.Controls.Add(search);

            ListBox list = new ListBox
            {
                Location = new Point(20, 75),
                Width = 420,
                Height = 320,
                Font = new Font("Segoe UI", 10F)
            };
            dlg.Controls.Add(list);

            void RefreshList()
            {
                list.Items.Clear();

                string key = RemoveVietnameseAccents(search.Text.Trim());

                foreach (var c in customerList)
                {
                    string hay = RemoveVietnameseAccents(c.Name + " " + c.Phone);
                    if (string.IsNullOrEmpty(key) || hay.Contains(key))
                        list.Items.Add(c);
                }
            }

            list.DisplayMember = "Name";
            list.Format += (s, e) =>
            {
                if (e.ListItem is Customer c)
                    e.Value = $"{c.Name}  |  {c.Phone}  |  {c.Points} điểm";
            };

            search.TextChanged += (s, e) => RefreshList();
            RefreshList();

            if (selectedCustomer != null)
            {
                int preIdx = -1;
                for (int i = 0; i < list.Items.Count; i++)
                {
                    if (list.Items[i] is Customer c && c == selectedCustomer)
                    {
                        preIdx = i;
                        break;
                    }
                }
                if (preIdx >= 0) list.SelectedIndex = preIdx;
            }

            list.DoubleClick += (s, e) => { dlg.DialogResult = DialogResult.OK; };

            Button ok = new Button
            {
                Text = "CHỌN",
                Location = new Point(255, 410),
                Width = 185,
                Height = 40,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(ok);

            Button cancel = new Button
            {
                Text = "HỦY",
                Location = new Point(20, 410),
                Width = 185,
                Height = 40,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            dlg.Controls.Add(cancel);

            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;

            if (dlg.ShowDialog(this) == DialogResult.OK && list.SelectedItem is Customer chosen)
            {
                SelectCustomer(chosen);
                return chosen;
            }

            return null;
        }

        private bool ShowRedeemDialog(
            Customer customer,
            decimal invoiceTotal,
            out int usedPoints,
            out decimal discount,
            out Product? reward)
        {
            usedPoints = 0;
            discount = 0;
            reward = null;

            List<Product> rewardList = productList
                .Where(p => p.IsReward && p.RewardPoints > 0)
                .OrderBy(p => p.RewardPoints)
                .ToList();

            Form dlg = new Form
            {
                Text = "Dùng điểm tích lũy",
                Size = new Size(520, 500),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label info = new Label
            {
                Text = $"Khách hàng: {customer.Name}\nĐiểm hiện có: {customer.Points}\nHóa đơn: {FormatVnd(invoiceTotal)}",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            dlg.Controls.Add(info);

            Label mode = new Label { Text = "Chọn cách sử dụng điểm:", Location = new Point(20, 95), AutoSize = true };
            dlg.Controls.Add(mode);

            RadioButton rbDiscount = new RadioButton
            {
                Text = "💰 Trừ điểm để giảm tiền hóa đơn",
                Location = new Point(20, 125),
                AutoSize = true,
                Checked = true
            };
            dlg.Controls.Add(rbDiscount);

            RadioButton rbGift = new RadioButton
            {
                Text = "🎁 Đổi một món quà (sản phẩm)",
                Location = new Point(20, 155),
                AutoSize = true,
                Enabled = rewardList.Count > 0
            };
            dlg.Controls.Add(rbGift);

            if (rewardList.Count == 0)
            {
                Label lblNoReward = new Label
                {
                    Text = "(Chưa có sản phẩm nào được đánh dấu đổi quà trong tab SẢN PHẨM)",
                    Location = new Point(40, 178),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(148, 163, 184),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
                };
                dlg.Controls.Add(lblNoReward);
            }

            Label lblPts = new Label { Text = "Số điểm muốn dùng:", Location = new Point(20, 200), AutoSize = true };
            dlg.Controls.Add(lblPts);

            int maxDiscountPoints = pointValueAmount > 0
                ? (int)Math.Floor(invoiceTotal / pointValueAmount)
                : 0;

            NumericUpDown nud = new NumericUpDown
            {
                Location = new Point(20, 225),
                Width = 180,
                Minimum = 0,
                Maximum = Math.Max(0, Math.Min(customer.Points, maxDiscountPoints)),
                ThousandsSeparator = true
            };
            dlg.Controls.Add(nud);

            Label lblMoney = new Label
            {
                Text = $"Giảm: {FormatVnd(0)}",
                Location = new Point(215, 230),
                AutoSize = true,
                ForeColor = Color.FromArgb(21, 128, 61),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            dlg.Controls.Add(lblMoney);

            Label lblGift = new Label { Text = "Chọn quà:", Location = new Point(20, 270), AutoSize = true, Visible = false };
            dlg.Controls.Add(lblGift);

            ComboBox cbGift = new ComboBox
            {
                Location = new Point(20, 295),
                Width = 430,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            foreach (var r in rewardList)
                cbGift.Items.Add($"{r.Name} | {r.RewardPoints} điểm");
            if (cbGift.Items.Count > 0) cbGift.SelectedIndex = 0;
            dlg.Controls.Add(cbGift);

            Label lblGiftInfo = new Label { Text = "", Location = new Point(20, 335), AutoSize = true, Visible = false };
            dlg.Controls.Add(lblGiftInfo);

            void ToggleMode()
            {
                bool isDiscount = rbDiscount.Checked;
                lblPts.Visible = isDiscount;
                nud.Visible = isDiscount;
                lblMoney.Visible = isDiscount;
                lblGift.Visible = !isDiscount;
                cbGift.Visible = !isDiscount;
                lblGiftInfo.Visible = !isDiscount;
            }

            rbDiscount.CheckedChanged += (s, e) => { ToggleMode(); };
            rbGift.CheckedChanged += (s, e) => { ToggleMode(); };

            nud.ValueChanged += (s, e) =>
            {
                decimal money = nud.Value * pointValueAmount;
                decimal maxDiscount = Math.Max(0, invoiceTotal);
                lblMoney.Text = $"Giảm: {FormatVnd(Math.Min(money, maxDiscount))}";
            };

            cbGift.SelectedIndexChanged += (s, e) =>
            {
                if (cbGift.SelectedIndex >= 0 && cbGift.SelectedIndex < rewardList.Count)
                {
                    Product r = rewardList[cbGift.SelectedIndex];
                    lblGiftInfo.Text = $"Cần {r.RewardPoints} điểm | Bạn đang có {customer.Points} điểm";
                }
            };

            if (cbGift.Items.Count > 0) cbGift.SelectedIndex = 0;

            Button btnOk = new Button
            {
                Text = "XÁC NHẬN",
                Location = new Point(265, 390),
                Width = 185,
                Height = 40,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnOk);

            Button btnCancel = new Button
            {
                Text = "HỦY",
                Location = new Point(20, 390),
                Width = 185,
                Height = 40,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return false;

            if (rbDiscount.Checked)
            {
                usedPoints = (int)nud.Value;

                if (usedPoints <= 0)
                {
                    MessageBox.Show("Số điểm sử dụng phải lớn hơn 0.", "Cảnh báo");
                    return false;
                }

                discount = Math.Min(usedPoints * pointValueAmount, invoiceTotal);
                return true;
            }

            if (cbGift.SelectedIndex < 0 || cbGift.SelectedIndex >= rewardList.Count)
            {
                MessageBox.Show("Vui lòng chọn quà.", "Cảnh báo");
                return false;
            }

            Product selectedReward = rewardList[cbGift.SelectedIndex];

            if (customer.Points < selectedReward.RewardPoints)
            {
                MessageBox.Show(
                    $"Không đủ điểm!\nCần {selectedReward.RewardPoints}, hiện có {customer.Points}.",
                    "Cảnh báo");
                return false;
            }

            reward = selectedReward;
            return true;
        }

        // ==========================================================
        // TAB SẢN PHẨM
        // ==========================================================

        private Panel BuildProductsView()
        {
            Panel viewContainer = new Panel { BackColor = Color.Transparent, AutoScroll = true };

            Panel pnlProductForm = new Panel
            {
                Location = new Point(0, 0),
                Width = 400,
                Height = 700,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Label lblTitle = new Label
            {
                Text = "📦 QUẢN LÝ SẢN PHẨM",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            pnlProductForm.Controls.Add(lblTitle);

            Label lblHint = new Label
            {
                Text = "Thêm sản phẩm thường dùng để khi tính tiền có thể chọn nhanh. Chuột phải vào sản phẩm bên phải để sửa/xóa.",
                Location = new Point(15, 50),
                Width = 340,
                Height = 35,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F)
            };
            pnlProductForm.Controls.Add(lblHint);

            Label lblName = new Label { Text = "Tên sản phẩm:", Location = new Point(15, 95), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            pnlProductForm.Controls.Add(lblName);

            txtProductName = new TextBox { Location = new Point(15, 118), Width = 340, Font = new Font("Segoe UI", 10.5F) };
            pnlProductForm.Controls.Add(txtProductName);

            Label lblPrice = new Label { Text = "Đơn giá (VNĐ):", Location = new Point(15, 158), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            pnlProductForm.Controls.Add(lblPrice);

            txtProductPrice = new TextBox { Location = new Point(15, 181), Width = 340, Font = new Font("Segoe UI", 10.5F) };
            pnlProductForm.Controls.Add(txtProductPrice);

            chkProductIsReward = new CheckBox
            {
                Text = "🎁 Cho phép đổi bằng điểm (đổi quà)",
                Location = new Point(15, 221),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            pnlProductForm.Controls.Add(chkProductIsReward);

            Label lblRewardPoints = new Label
            {
                Text = "Số điểm cần để đổi sản phẩm này:",
                Location = new Point(15, 250),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlProductForm.Controls.Add(lblRewardPoints);

            numProductRewardPoints = new NumericUpDown
            {
                Location = new Point(15, 273),
                Width = 195,
                Minimum = 0,
                Maximum = 1000000,
                Increment = 5,
                Enabled = false,
                ThousandsSeparator = true
            };
            pnlProductForm.Controls.Add(numProductRewardPoints);

            Button btnAutoRewardPoints = new Button
            {
                Text = "🔧 Tự Tính Theo Giá",
                Location = new Point(220, 271),
                Width = 135,
                Height = 27,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnAutoRewardPoints.FlatAppearance.BorderSize = 0;
            btnAutoRewardPoints.Click += (s, e) =>
            {
                if (decimal.TryParse(txtProductPrice.Text, out decimal price) && price > 0 && pointValueAmount > 0)
                {
                    int suggested = (int)Math.Ceiling(price / pointValueAmount);
                    numProductRewardPoints.Value = Math.Min(numProductRewardPoints.Maximum, Math.Max(numProductRewardPoints.Minimum, suggested));
                }
                else
                {
                    MessageBox.Show("Vui lòng nhập đơn giá hợp lệ trước khi tự tính điểm!", "Cảnh báo");
                }
            };
            pnlProductForm.Controls.Add(btnAutoRewardPoints);

            chkProductIsReward.CheckedChanged += (s, e) =>
            {
                numProductRewardPoints.Enabled = chkProductIsReward.Checked;
                btnAutoRewardPoints.Enabled = chkProductIsReward.Checked;
                if (!chkProductIsReward.Checked) numProductRewardPoints.Value = 0;
            };

            Label lblSuggestRewardPts = new Label
            {
                Text = "Gợi ý nhanh (điểm):",
                Location = new Point(15, 305),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            pnlProductForm.Controls.Add(lblSuggestRewardPts);

            int[] pointPresets = { 10, 25, 50, 100, 200 };
            int px = 15;
            foreach (int pts in pointPresets)
            {
                Button btnPtPreset = new Button
                {
                    Text = pts.ToString(),
                    Location = new Point(px, 328),
                    Width = 62,
                    Height = 28,
                    BackColor = Color.FromArgb(226, 232, 240),
                    ForeColor = Color.FromArgb(51, 65, 85),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnPtPreset.FlatAppearance.BorderSize = 0;
                int capturedPts = pts;
                btnPtPreset.Click += (s, e) =>
                {
                    if (chkProductIsReward.Checked) numProductRewardPoints.Value = capturedPts;
                };
                pnlProductForm.Controls.Add(btnPtPreset);
                px += 68;
            }

            Label lblRewardHint = new Label
            {
                Text = "💡 Mẹo: điểm đổi quà nên ≈ giá bán ÷ giá trị 1 điểm (xem tab Cài Đặt), tránh trường hợp đổi quà lời/lỗ hẳn so với dùng điểm giảm giá hóa đơn.",
                Location = new Point(15, 362),
                Width = 340,
                Height = 48,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic)
            };
            pnlProductForm.Controls.Add(lblRewardHint);

            Label lblProductNoteTitle = new Label
            {
                Text = "Ghi chú (tuỳ chọn):",
                Location = new Point(15, 415),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlProductForm.Controls.Add(lblProductNoteTitle);

            txtProductNote = new TextBox
            {
                Location = new Point(15, 438),
                Width = 340,
                Height = 55,
                Multiline = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlProductForm.Controls.Add(txtProductNote);

            Button btnAddProduct = new Button
            {
                Text = "＋ THÊM SẢN PHẨM",
                Location = new Point(15, 505),
                Width = 340,
                Height = 42,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnAddProduct.FlatAppearance.BorderSize = 0;
            btnAddProduct.Click += BtnAddProduct_Click;
            pnlProductForm.Controls.Add(btnAddProduct);

            Button btnRemoveProduct = new Button
            {
                Text = "🗑 XÓA SẢN PHẨM ĐÃ CHỌN",
                Location = new Point(15, 558),
                Width = 340,
                Height = 38,
                BackColor = Color.FromArgb(248, 113, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRemoveProduct.FlatAppearance.BorderSize = 0;
            btnRemoveProduct.Click += BtnRemoveProduct_Click;
            pnlProductForm.Controls.Add(btnRemoveProduct);

            Label lblProductTip = new Label
            {
                Text = "Mẹo: sản phẩm đánh dấu \"đổi quà\" sẽ xuất hiện trong bước dùng điểm khi thanh toán hóa đơn.",
                Location = new Point(15, 610),
                Width = 340,
                Height = 60,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };
            pnlProductForm.Controls.Add(lblProductTip);

            Panel pnlProductList = new Panel
            {
                Location = new Point(420, 0),
                Width = 660,
                Height = 700,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Label lblListTitle = new Label
            {
                Text = "📋 DANH SÁCH SẢN PHẨM ĐÃ THÊM (Nhấp đúp hoặc chuột phải để Sửa / Xóa)",
                Dock = DockStyle.Top,
                Height = 35,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            lvProducts = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 10F),
                ShowItemToolTips = true
            };
            lvProducts.Columns.Add("Tên sản phẩm", 220);
            lvProducts.Columns.Add("Đơn giá", 120);
            lvProducts.Columns.Add("Đổi quà", 80);
            lvProducts.Columns.Add("Điểm cần", 80);
            lvProducts.Columns.Add("Ghi chú", 150);

            // Sự kiện chuột phải (hiện menu)
            lvProducts.MouseUp += LvProducts_MouseUp;
            // Sự kiện nhấp đúp (mở thẳng form sửa)
            lvProducts.DoubleClick += LvProducts_DoubleClick;

            pnlProductList.Controls.Add(lvProducts);
            pnlProductList.Controls.Add(lblListTitle);
            lvProducts.BringToFront();

            viewContainer.Controls.Add(pnlProductForm);
            viewContainer.Controls.Add(pnlProductList);

            return viewContainer;
        }

        private void LvProducts_DoubleClick(object? sender, EventArgs e)
        {
            if (lvProducts.SelectedItems.Count == 0) return;

            ListViewItem item = lvProducts.SelectedItems[0];

            List<Product> sortedProducts = productList.OrderBy(p => p.Name).ToList();
            int idx = item.Index;
            if (idx < 0 || idx >= sortedProducts.Count) return;

            Product product = sortedProducts[idx];

            // Mở thẳng form sửa
            ShowEditProductDialog(product);
        }

        private void LvProducts_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            ListViewItem? item = lvProducts.GetItemAt(e.X, e.Y);
            if (item == null) return;

            item.Selected = true;

            List<Product> sortedProducts = productList.OrderBy(p => p.Name).ToList();
            int idx = item.Index;
            if (idx < 0 || idx >= sortedProducts.Count) return;

            Product product = sortedProducts[idx];

            ContextMenuStrip cm = new ContextMenuStrip();

            ToolStripMenuItem miEdit = new ToolStripMenuItem("✏️ Sửa sản phẩm");
            miEdit.Click += (s, ev) => { ShowEditProductDialog(product); };
            cm.Items.Add(miEdit);

            cm.Show(lvProducts, e.Location);
        }

        private void ShowEditProductDialog(Product product)
        {
            Form dlg = new Form
            {
                Text = "Sửa sản phẩm",
                Size = new Size(460, 560),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblTitle = new Label
            {
                Text = "✏️ SỬA SẢN PHẨM",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            dlg.Controls.Add(lblTitle);

            Label lblName = new Label { Text = "Tên sản phẩm:", Location = new Point(20, 55), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblName);
            TextBox txtName = new TextBox { Location = new Point(20, 78), Width = 400, Text = product.Name, Font = new Font("Segoe UI", 10.5F) };
            dlg.Controls.Add(txtName);

            Label lblPrice = new Label { Text = "Đơn giá (VNĐ):", Location = new Point(20, 115), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblPrice);
            TextBox txtPrice = new TextBox { Location = new Point(20, 138), Width = 400, Text = product.UnitPrice.ToString("0", CultureInfo.InvariantCulture), Font = new Font("Segoe UI", 10.5F) };
            dlg.Controls.Add(txtPrice);

            CheckBox chkReward = new CheckBox
            {
                Text = "🎁 Cho phép đổi bằng điểm (đổi quà)",
                Location = new Point(20, 178),
                AutoSize = true,
                Checked = product.IsReward,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            dlg.Controls.Add(chkReward);

            Label lblPts = new Label { Text = "Số điểm cần để đổi:", Location = new Point(20, 208), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblPts);

            NumericUpDown numPts = new NumericUpDown
            {
                Location = new Point(20, 231),
                Width = 180,
                Minimum = 0,
                Maximum = 1000000,
                Increment = 5,
                Value = product.RewardPoints,
                Enabled = product.IsReward,
                ThousandsSeparator = true
            };
            dlg.Controls.Add(numPts);

            Button btnAutoCalc = new Button
            {
                Text = "🔧 Tự Tính Theo Giá",
                Location = new Point(210, 230),
                Width = 210,
                Height = 26,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = product.IsReward
            };
            btnAutoCalc.FlatAppearance.BorderSize = 0;
            btnAutoCalc.Click += (s, e) =>
            {
                if (decimal.TryParse(txtPrice.Text, out decimal p) && p > 0 && pointValueAmount > 0)
                {
                    int suggested = (int)Math.Ceiling(p / pointValueAmount);
                    numPts.Value = Math.Min(numPts.Maximum, Math.Max(numPts.Minimum, suggested));
                }
                else
                {
                    MessageBox.Show("Vui lòng nhập đơn giá hợp lệ trước!", "Cảnh báo");
                }
            };
            dlg.Controls.Add(btnAutoCalc);

            chkReward.CheckedChanged += (s, e) =>
            {
                numPts.Enabled = chkReward.Checked;
                btnAutoCalc.Enabled = chkReward.Checked;
                if (!chkReward.Checked) numPts.Value = 0;
            };

            Label lblHint = new Label
            {
                Text = "💡 Gợi ý: điểm đổi quà nên ≈ giá bán ÷ giá trị 1 điểm (xem tab Cài Đặt) để hợp lý so với việc dùng điểm giảm giá hóa đơn.",
                Location = new Point(20, 265),
                Width = 400,
                Height = 45,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic)
            };
            dlg.Controls.Add(lblHint);

            Label lblNote = new Label { Text = "Ghi chú:", Location = new Point(20, 318), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            dlg.Controls.Add(lblNote);

            TextBox txtNote = new TextBox
            {
                Location = new Point(20, 341),
                Width = 400,
                Height = 70,
                Multiline = true,
                Text = product.Note,
                Font = new Font("Segoe UI", 10F)
            };
            dlg.Controls.Add(txtNote);

            // NÚT XÓA 
            Button btnDelete = new Button
            {
                Text = "🗑 XÓA",
                Location = new Point(20, 435),
                Width = 110,
                Height = 40,
                BackColor = Color.FromArgb(248, 113, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += (s, e) =>
            {
                DialogResult result = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa sản phẩm '{product.Name}' không?\nHành động này không thể hoàn tác.",
                    "Xác nhận xóa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    productList.Remove(product);
                    RefreshProductList();
                    RefreshSavedProductsCombo(txtProductSearchBill != null ? txtProductSearchBill.Text : "");
                    MessageBox.Show("Đã xóa sản phẩm!", "Thành công");

                    dlg.DialogResult = DialogResult.Abort;
                    dlg.Close();
                }
            };
            dlg.Controls.Add(btnDelete);

            // NÚT HỦY
            Button btnCancel = new Button
            {
                Text = "HỦY",
                Location = new Point(140, 435),
                Width = 110,
                Height = 40,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            dlg.Controls.Add(btnCancel);

            // NÚT LƯU
            Button btnSave = new Button
            {
                Text = "💾 LƯU",
                Location = new Point(260, 435),
                Width = 160,
                Height = 40,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnSave);

            dlg.AcceptButton = btnSave;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string newName = txtName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Vui lòng nhập tên sản phẩm!", "Cảnh báo");
                return;
            }

            if (!decimal.TryParse(txtPrice.Text, out decimal newPrice) || newPrice <= 0)
            {
                MessageBox.Show("Vui lòng nhập đơn giá hợp lệ!", "Cảnh báo");
                return;
            }

            bool newIsReward = chkReward.Checked;
            int newRewardPoints = newIsReward ? (int)numPts.Value : 0;

            if (newIsReward && newRewardPoints <= 0)
            {
                MessageBox.Show("Nếu cho phép đổi quà thì số điểm cần phải > 0.", "Cảnh báo");
                return;
            }

            bool nameUsedByOther = productList.Any(p => p != product && string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase));
            if (nameUsedByOther)
            {
                MessageBox.Show("Tên sản phẩm này đã được dùng bởi sản phẩm khác!", "Cảnh báo");
                return;
            }

            product.Name = newName;
            product.UnitPrice = newPrice;
            product.IsReward = newIsReward;
            product.RewardPoints = newRewardPoints;
            product.Note = txtNote.Text.Trim();

            RefreshProductList();
            RefreshSavedProductsCombo(txtProductSearchBill != null ? txtProductSearchBill.Text : "");

            MessageBox.Show("Đã cập nhật sản phẩm!", "Thành công");
        }

        private void DeleteProductWithConfirm(Product product)
        {
            DialogResult result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa sản phẩm '{product.Name}' không?\nHành động này không thể hoàn tác.",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            productList.Remove(product);

            RefreshProductList();
            RefreshSavedProductsCombo(txtProductSearchBill != null ? txtProductSearchBill.Text : "");

            MessageBox.Show("Đã xóa sản phẩm!", "Thành công");
        }

        private void BtnAddProduct_Click(object? sender, EventArgs e)
        {
            string name = txtProductName.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Vui lòng nhập tên sản phẩm!", "Cảnh báo");
                return;
            }

            if (!decimal.TryParse(txtProductPrice.Text, out decimal price) || price <= 0)
            {
                MessageBox.Show("Vui lòng nhập đơn giá hợp lệ!", "Cảnh báo");
                return;
            }

            bool isReward = chkProductIsReward.Checked;
            int rewardPoints = isReward ? (int)numProductRewardPoints.Value : 0;

            if (isReward && rewardPoints <= 0)
            {
                MessageBox.Show("Nếu cho phép đổi quà thì số điểm cần phải > 0.", "Cảnh báo");
                return;
            }

            string note = txtProductNote.Text.Trim();

            Product? existing = productList.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.UnitPrice = price;
                existing.IsReward = isReward;
                existing.RewardPoints = rewardPoints;
                existing.Note = note;

                MessageBox.Show($"Sản phẩm '{name}' đã tồn tại. Đã cập nhật thông tin!", "Thông báo");
            }
            else
            {
                productList.Add(new Product
                {
                    Name = name,
                    UnitPrice = price,
                    IsReward = isReward,
                    RewardPoints = rewardPoints,
                    Note = note
                });

                MessageBox.Show($"Đã thêm sản phẩm: {name}", "Thành công");
            }

            RefreshProductList();
            RefreshSavedProductsCombo(txtProductSearchBill != null ? txtProductSearchBill.Text : "");

            txtProductName.Clear();
            txtProductPrice.Clear();
            chkProductIsReward.Checked = false;
            numProductRewardPoints.Value = 0;
            txtProductNote.Clear();
            txtProductName.Focus();
        }

        private void BtnRemoveProduct_Click(object? sender, EventArgs e)
        {
            if (lvProducts.SelectedIndices.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn sản phẩm cần xóa!", "Cảnh báo");
                return;
            }

            int idx = lvProducts.SelectedIndices[0];
            List<Product> sortedProducts = productList.OrderBy(p => p.Name).ToList();

            if (idx < 0 || idx >= sortedProducts.Count) return;

            Product product = sortedProducts[idx];
            DeleteProductWithConfirm(product);
        }

        private void RefreshProductList()
        {
            if (lvProducts == null) return;

            lvProducts.Items.Clear();

            foreach (Product product in productList.OrderBy(p => p.Name))
            {
                ListViewItem lvi = new ListViewItem(product.Name);
                lvi.SubItems.Add(FormatVnd(product.UnitPrice));
                lvi.SubItems.Add(product.IsReward ? "Có" : "Không");
                lvi.SubItems.Add(product.IsReward ? product.RewardPoints.ToString() : "-");
                lvi.SubItems.Add(string.IsNullOrEmpty(product.Note) ? "-" : product.Note);
                lvi.ToolTipText = string.IsNullOrEmpty(product.Note) ? "" : product.Note;
                lvProducts.Items.Add(lvi);
            }
        }

        // ==========================================================
        // TAB CÀI ĐẶT
        // ==========================================================

        private Panel BuildSettingsView()
        {
            Panel viewContainer = new Panel { BackColor = Color.Transparent, AutoScroll = true };

            // ======================================================
            // CÀI ĐẶT ĐIỂM
            // ======================================================

            Panel pnlPointsSettings = new Panel
            {
                Location = new Point(0, 0),
                Width = 480,
                Height = 470,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Label lblPointsSettingsTitle = new Label
            {
                Text = "⚙️ CÀI ĐẶT HỆ SỐ TÍCH / DÙNG ĐIỂM",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            pnlPointsSettings.Controls.Add(lblPointsSettingsTitle);

            Label lblRatioDesc = new Label
            {
                Text = "Số tiền (VNĐ) cần chi để được cộng 1 điểm:",
                Location = new Point(15, 55),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlPointsSettings.Controls.Add(lblRatioDesc);

            numPointsRatio = new NumericUpDown
            {
                Location = new Point(15, 78),
                Width = 200,
                Minimum = 1000,
                Maximum = 10000000,
                Increment = 10000,
                Value = pointsRatioAmount,
                Font = new Font("Segoe UI", 11F),
                ThousandsSeparator = true
            };
            pnlPointsSettings.Controls.Add(numPointsRatio);

            Label lblRatioSuggest = new Label
            {
                Text = "Gợi ý nhanh:",
                Location = new Point(15, 118),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            pnlPointsSettings.Controls.Add(lblRatioSuggest);

            decimal[] ratioSuggestions = { 50000m, 100000m, 200000m, 500000m };
            int rx = 15;
            foreach (decimal suggest in ratioSuggestions)
            {
                Button btnRatioSuggest = new Button
                {
                    Text = suggest.ToString("#,##0", CultureInfo.InvariantCulture),
                    Location = new Point(rx, 141),
                    Width = 100,
                    Height = 28,
                    BackColor = Color.FromArgb(226, 232, 240),
                    ForeColor = Color.FromArgb(51, 65, 85),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnRatioSuggest.FlatAppearance.BorderSize = 0;
                decimal capturedRatio = suggest;
                btnRatioSuggest.Click += (s, e) => { numPointsRatio.Value = capturedRatio; };
                pnlPointsSettings.Controls.Add(btnRatioSuggest);
                rx += 105;
            }

            Label lblValueDesc = new Label
            {
                Text = "Giá trị quy đổi khi dùng 1 điểm để giảm hóa đơn (VNĐ):",
                Location = new Point(15, 183),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlPointsSettings.Controls.Add(lblValueDesc);

            numPointValue = new NumericUpDown
            {
                Location = new Point(15, 206),
                Width = 200,
                Minimum = 100,
                Maximum = 1000000,
                Increment = 100,
                Value = pointValueAmount,
                Font = new Font("Segoe UI", 11F),
                ThousandsSeparator = true
            };
            pnlPointsSettings.Controls.Add(numPointValue);

            Label lblValueSuggest = new Label
            {
                Text = "Gợi ý nhanh:",
                Location = new Point(15, 246),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            pnlPointsSettings.Controls.Add(lblValueSuggest);

            decimal[] valueSuggestions = { 500m, 1000m, 2000m, 5000m, 10000m };
            int vx = 15;
            foreach (decimal val in valueSuggestions)
            {
                Button btnValueSuggest = new Button
                {
                    Text = val.ToString("#,##0", CultureInfo.InvariantCulture) + "đ",
                    Location = new Point(vx, 269),
                    Width = 80,
                    Height = 28,
                    BackColor = Color.FromArgb(226, 232, 240),
                    ForeColor = Color.FromArgb(51, 65, 85),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnValueSuggest.FlatAppearance.BorderSize = 0;
                decimal capturedVal = val;
                btnValueSuggest.Click += (s, e) => { numPointValue.Value = capturedVal; };
                pnlPointsSettings.Controls.Add(btnValueSuggest);
                vx += 85;
            }

            Label lblExample = new Label
            {
                Text = "Ví dụ: 100.000 VNĐ = 1 điểm; 1 điểm = 1.000 VNĐ giảm hóa đơn.",
                Location = new Point(15, 307),
                Width = 340,
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            pnlPointsSettings.Controls.Add(lblExample);

            Button btnSavePointsRatio = new Button
            {
                Text = "💾 LƯU CÀI ĐẶT ĐIỂM",
                Location = new Point(15, 344),
                Width = 330,
                Height = 42,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSavePointsRatio.FlatAppearance.BorderSize = 0;
            btnSavePointsRatio.Click += BtnSavePointsRatio_Click;
            pnlPointsSettings.Controls.Add(btnSavePointsRatio);

            Label lblCurrentSettings = new Label
            {
                Text = "",
                Name = "lblCurrentSettings",
                Location = new Point(15, 398),
                Width = 340,
                Height = 60,
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 9F)
            };
            pnlPointsSettings.Controls.Add(lblCurrentSettings);
            UpdateCurrentSettingsLabel(lblCurrentSettings);

            // ======================================================
            // COUPON
            // ======================================================

            Panel pnlCouponSettings = new Panel
            {
                Location = new Point(0, 485),
                Width = 480,
                Height = 310,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Label lblCouponTitle = new Label
            {
                Text = "🎟️ CÀI ĐẶT MÃ COUPON",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            pnlCouponSettings.Controls.Add(lblCouponTitle);

            Label lblCouponPurpose = new Label
            {
                Text = "Tên / mục đích mã coupon:",
                Location = new Point(15, 55),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlCouponSettings.Controls.Add(lblCouponPurpose);

            txtCouponPurpose = new TextBox
            {
                Location = new Point(15, 78),
                Width = 330,
                Font = new Font("Segoe UI", 10.5F)
            };
            pnlCouponSettings.Controls.Add(txtCouponPurpose);

            Label lblCouponDaysDesc = new Label
            {
                Text = "Thời hạn sử dụng mã (số ngày kể từ lúc tạo):",
                Location = new Point(15, 115),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            pnlCouponSettings.Controls.Add(lblCouponDaysDesc);

            numCouponDays = new NumericUpDown
            {
                Location = new Point(15, 138),
                Width = 200,
                Minimum = 1,
                Maximum = 3650,
                Value = 30,
                Font = new Font("Segoe UI", 11F)
            };
            pnlCouponSettings.Controls.Add(numCouponDays);

            Label lblSuggestDays = new Label
            {
                Text = "Gợi ý nhanh:",
                Location = new Point(15, 180),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            pnlCouponSettings.Controls.Add(lblSuggestDays);

            int[] daySuggestions = { 7, 15, 30, 60, 90 };
            int dx = 15;

            foreach (int days in daySuggestions)
            {
                Button btnSuggestDay = new Button
                {
                    Text = $"{days} ngày",
                    Location = new Point(dx, 203),
                    Width = 85,
                    Height = 30,
                    BackColor = Color.FromArgb(226, 232, 240),
                    ForeColor = Color.FromArgb(51, 65, 85),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnSuggestDay.FlatAppearance.BorderSize = 0;

                int capturedDays = days;
                btnSuggestDay.Click += (s, e) => { numCouponDays.Value = capturedDays; };

                pnlCouponSettings.Controls.Add(btnSuggestDay);
                dx += 90;
            }

            Button btnGenerateCoupon = new Button
            {
                Text = "✨ Tạo Mã Coupon Mới",
                Location = new Point(15, 245),
                Width = 330,
                Height = 40,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnGenerateCoupon.FlatAppearance.BorderSize = 0;
            btnGenerateCoupon.Click += BtnGenerateCoupon_Click;
            pnlCouponSettings.Controls.Add(btnGenerateCoupon);

            // ======================================================
            // DANH SÁCH COUPON
            // ======================================================

            Panel pnlCouponListBox = new Panel
            {
                Location = new Point(500, 0),
                Width = 400,
                Height = 795,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            Label lblCouponListTitle = new Label
            {
                Text = "📋 DANH SÁCH MÃ ĐÃ TẠO",
                Dock = DockStyle.Top,
                Height = 35,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            lvCoupons = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            lvCoupons.Columns.Add("Mã Coupon", 120);
            lvCoupons.Columns.Add("Mục đích", 150);
            lvCoupons.Columns.Add("Ngày tạo", 90);
            lvCoupons.Columns.Add("Hết hạn", 90);

            pnlCouponListBox.Controls.Add(lvCoupons);
            pnlCouponListBox.Controls.Add(lblCouponListTitle);
            lvCoupons.BringToFront();

            viewContainer.Controls.Add(pnlPointsSettings);
            viewContainer.Controls.Add(pnlCouponSettings);
            viewContainer.Controls.Add(pnlCouponListBox);

            return viewContainer;
        }

        private void UpdateCurrentSettingsLabel(Label lbl)
        {
            lbl.Text =
                $"Đang áp dụng:\n" +
                $"• {FormatVnd(pointsRatioAmount)} = 1 điểm\n" +
                $"• 1 điểm = {FormatVnd(pointValueAmount)} khi giảm hóa đơn";
        }

        private void BtnSavePointsRatio_Click(object? sender, EventArgs e)
        {
            pointsRatioAmount = numPointsRatio.Value;
            pointValueAmount = numPointValue.Value;

            TxtQuickAmountInput_TextChanged(null, EventArgs.Empty);

            Control? lbl = numPointsRatio.Parent?.Controls["lblCurrentSettings"];
            if (lbl is Label label) UpdateCurrentSettingsLabel(label);

            MessageBox.Show(
                $"Đã lưu:\n• {FormatVnd(pointsRatioAmount)} = 1 điểm\n• 1 điểm = {FormatVnd(pointValueAmount)} giảm hóa đơn",
                "Thành công");
        }

        private void BtnGenerateCoupon_Click(object? sender, EventArgs e)
        {
            string purpose = txtCouponPurpose.Text.Trim();

            if (string.IsNullOrEmpty(purpose))
            {
                MessageBox.Show("Vui lòng nhập tên / mục đích của mã coupon!", "Cảnh báo");
                txtCouponPurpose.Focus();
                return;
            }

            string code = GenerateCouponCode();
            DateTime created = DateTime.Now;
            DateTime expiry = created.AddDays((double)numCouponDays.Value);

            couponList.Add(new Coupon
            {
                Code = code,
                Purpose = purpose,
                CreatedDate = created,
                ExpiryDate = expiry
            });

            RefreshCouponList();
            txtCouponPurpose.Clear();

            MessageBox.Show(
                $"Đã tạo mã coupon mới:\n\nMã: {code}\nMục đích: {purpose}\nNgày tạo: {created:dd/MM/yyyy}\nHết hạn: {expiry:dd/MM/yyyy}",
                "Thành công");
        }

        private string GenerateCouponCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new StringBuilder();

            for (int i = 0; i < 8; i++)
                sb.Append(chars[couponRandom.Next(chars.Length)]);

            return sb.ToString();
        }

        private void RefreshCouponList()
        {
            if (lvCoupons == null) return;

            lvCoupons.Items.Clear();

            foreach (Coupon c in couponList.OrderByDescending(x => x.CreatedDate))
            {
                ListViewItem lvi = new ListViewItem(c.Code);
                lvi.SubItems.Add(c.Purpose);
                lvi.SubItems.Add(c.CreatedDate.ToString("dd/MM/yyyy"));
                lvi.SubItems.Add(c.ExpiryDate.ToString("dd/MM/yyyy"));
                lvCoupons.Items.Add(lvi);
            }
        }

        // ==========================================================
        // TAB HÓA ĐƠN (xem TẤT CẢ hóa đơn của TẤT CẢ khách hàng)
        // ==========================================================

        private Panel BuildInvoicesView()
        {
            Panel viewContainer = new Panel { BackColor = Color.White, Padding = new Padding(20) };

            Label lblTitle = new Label
            {
                Text = "📜 DANH SÁCH TẤT CẢ HÓA ĐƠN",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199)
            };
            viewContainer.Controls.Add(lblTitle);

            rbInvAll = new RadioButton
            {
                Text = "Tất cả (mặc định — mới nhất trước)",
                Location = new Point(15, 55),
                AutoSize = true,
                Checked = true
            };
            viewContainer.Controls.Add(rbInvAll);

            rbInvDay = new RadioButton { Text = "Theo ngày:", Location = new Point(15, 85), AutoSize = true };
            viewContainer.Controls.Add(rbInvDay);

            dtpInvDay = new DateTimePicker
            {
                Location = new Point(145, 82),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            viewContainer.Controls.Add(dtpInvDay);

            rbInvMonth = new RadioButton { Text = "Theo tháng:", Location = new Point(15, 118), AutoSize = true };
            viewContainer.Controls.Add(rbInvMonth);

            numInvMonth = new NumericUpDown
            {
                Location = new Point(145, 115),
                Width = 60,
                Minimum = 1,
                Maximum = 12,
                Value = DateTime.Now.Month
            };
            viewContainer.Controls.Add(numInvMonth);

            Label lblSlash = new Label { Text = "/", Location = new Point(210, 118), AutoSize = true };
            viewContainer.Controls.Add(lblSlash);

            numInvMonthYear = new NumericUpDown
            {
                Location = new Point(227, 115),
                Width = 80,
                Minimum = 2000,
                Maximum = 2100,
                Value = DateTime.Now.Year
            };
            viewContainer.Controls.Add(numInvMonthYear);

            rbInvYear = new RadioButton { Text = "Theo năm:", Location = new Point(15, 151), AutoSize = true };
            viewContainer.Controls.Add(rbInvYear);

            numInvYear = new NumericUpDown
            {
                Location = new Point(145, 148),
                Width = 90,
                Minimum = 2000,
                Maximum = 2100,
                Value = DateTime.Now.Year
            };
            viewContainer.Controls.Add(numInvYear);

            rbInvRange = new RadioButton { Text = "Khoảng ngày:", Location = new Point(15, 184), AutoSize = true };
            viewContainer.Controls.Add(rbInvRange);

            dtpInvFrom = new DateTimePicker
            {
                Location = new Point(145, 181),
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddDays(-30)
            };
            viewContainer.Controls.Add(dtpInvFrom);

            Label lblTo = new Label { Text = "đến", Location = new Point(293, 184), AutoSize = true };
            viewContainer.Controls.Add(lblTo);

            dtpInvTo = new DateTimePicker
            {
                Location = new Point(325, 181),
                Width = 140,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            viewContainer.Controls.Add(dtpInvTo);

            Button btnApplyFilter = new Button
            {
                Text = "🔍 LỌC",
                Location = new Point(510, 82),
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnApplyFilter.FlatAppearance.BorderSize = 0;
            btnApplyFilter.Click += (s, e) => { RefreshAllInvoicesList(); };
            viewContainer.Controls.Add(btnApplyFilter);

            Button btnResetFilter = new Button
            {
                Text = "Xem Tất Cả",
                Location = new Point(510, 122),
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResetFilter.FlatAppearance.BorderSize = 0;
            btnResetFilter.Click += (s, e) => { rbInvAll.Checked = true; RefreshAllInvoicesList(); };
            viewContainer.Controls.Add(btnResetFilter);

            Panel pnlDivider = new Panel { Location = new Point(15, 222), Width = 1000, Height = 1, BackColor = Color.FromArgb(226, 232, 240) };
            viewContainer.Controls.Add(pnlDivider);

            lvAllInvoices = new ListView
            {
                Location = new Point(15, 234),
                Width = 1050,
                Height = 400,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9.5F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            lvAllInvoices.Columns.Add("Ngày giờ", 110);
            lvAllInvoices.Columns.Add("Khách hàng", 200);
            lvAllInvoices.Columns.Add("Loại", 110);
            lvAllInvoices.Columns.Add("Sản phẩm", 260);
            lvAllInvoices.Columns.Add("Tổng tiền", 100);
            lvAllInvoices.Columns.Add("Giảm giá", 90);
            lvAllInvoices.Columns.Add("Đổi quà", 100);
            lvAllInvoices.Columns.Add("Dùng điểm", 70);
            viewContainer.Controls.Add(lvAllInvoices);

            lblAllInvoicesSummary = new Label
            {
                Text = "",
                Location = new Point(15, 645),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61)
            };
            viewContainer.Controls.Add(lblAllInvoicesSummary);

            return viewContainer;
        }

        private void RefreshAllInvoicesList()
        {
            if (lvAllInvoices == null) return;

            List<Invoice> source = invoiceList;
            List<Invoice> filtered;

            if (rbInvDay.Checked)
            {
                DateTime day = dtpInvDay.Value.Date;
                filtered = source.Where(i => i.Date.Date == day).ToList();
            }
            else if (rbInvMonth.Checked)
            {
                int month = (int)numInvMonth.Value;
                int year = (int)numInvMonthYear.Value;
                filtered = source.Where(i => i.Date.Month == month && i.Date.Year == year).ToList();
            }
            else if (rbInvYear.Checked)
            {
                int year = (int)numInvYear.Value;
                filtered = source.Where(i => i.Date.Year == year).ToList();
            }
            else if (rbInvRange.Checked)
            {
                DateTime from = dtpInvFrom.Value.Date;
                DateTime to = dtpInvTo.Value.Date;
                filtered = source.Where(i => i.Date.Date >= from && i.Date.Date <= to).ToList();
            }
            else
            {
                filtered = source;
            }

            filtered = filtered.OrderByDescending(i => i.Date).ToList();

            lvAllInvoices.Items.Clear();
            decimal sumFinal = 0;

            foreach (Invoice inv in filtered)
            {
                string itemsText = inv.Items.Count > 0
                    ? string.Join(", ", inv.Items.Select(x => $"{x.Name} x{x.Qty}"))
                    : "-";

                Customer? cust = customerList.FirstOrDefault(c => c.Phone == inv.CustomerPhone);
                string custText = cust != null ? $"{cust.Name} ({cust.Phone})" : inv.CustomerPhone;

                ListViewItem lvi = new ListViewItem(inv.Date.ToString("dd/MM/yyyy HH:mm"));
                lvi.SubItems.Add(custText);
                lvi.SubItems.Add(inv.Type);
                lvi.SubItems.Add(itemsText);
                lvi.SubItems.Add(FormatVnd(inv.FinalTotal));
                lvi.SubItems.Add(inv.Discount > 0 ? FormatVnd(inv.Discount) : "-");
                lvi.SubItems.Add(string.IsNullOrEmpty(inv.RewardName) ? "-" : inv.RewardName);
                lvi.SubItems.Add(inv.PointsUsed > 0 ? inv.PointsUsed.ToString() : "-");
                lvAllInvoices.Items.Add(lvi);

                sumFinal += inv.FinalTotal;
            }

            lblAllInvoicesSummary.Text = $"Tổng số hóa đơn: {filtered.Count}    |    Tổng tiền: {FormatVnd(sumFinal)}";
        }
    }
}