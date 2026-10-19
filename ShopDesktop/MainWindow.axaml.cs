using System.Numerics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopDesktop.Models;

namespace ShopDesktop;

// Код окна. Здесь — что происходит, когда пользователь что-то нажал или выбрал.
// Элементы окна (ProductsGrid, SearchBox, ...) описаны в MainWindow.axaml, здесь к ним обращаемся по имени.
public partial class MainWindow : Window
{
    // Все товары, которые пришли из API. Фильтры выбирают товары из этого списка, сам список не меняется
    private List<Product> allProducts = new();

    public MainWindow()
    {
        InitializeComponent();
        StatusBox.ItemsSource = new[] { "Новый", "Оплачен", "Доставлен", "Отменён" };
    }

    // Окно открылось — загружаем данные из API. Готово, ошибок нет
    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            await LoadCategories();
            await LoadProducts();
            await LoadOrders();
        }
        catch (HttpRequestException)
        {
            ShowMessage("Не получилось связаться с API. Запусти ShopAPI (README, шаг 2) и перезапусти программу");
        }
    }

    // Показать сообщение в строке внизу окна
    private void ShowMessage(string text)
    {
        MessageText.Text = text;
    }

    // ============ Часть A. Товары ============

    // Пример. Загружаем категории в оба выпадающих списка. Готово, ошибок нет
    private async Task LoadCategories()
    {
        var categories = await Api.GetCategories();

        // В форме «Новый товар» — только настоящие категории
        NewCategoryBox.ItemsSource = categories;

        // В фильтре первой строкой ещё и «Все категории». Её Id = 0 — такой категории в базе нет
        var withAll = new List<Category> { new Category { Id = 0, Name = "Все категории" } };
        withAll.AddRange(categories);
        CategoryBox.ItemsSource = withAll;
        CategoryBox.SelectedIndex = 0;
    }

    // Загружаем товары из API и показываем их с учётом фильтров. Готово, ошибок нет
    private async Task LoadProducts()
    {
        allProducts = await Api.GetProducts();
        ApplyFilter();
    }

    // Пользователь поменял любой фильтр — пересчитываем таблицу. Готово, ошибок нет
    private void Filter_Changed(object? sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    // Задания 2–5. Показывает в таблице только товары, которые подходят под ВСЕ фильтры сразу.
    // Фильтры работают по очереди: каждый отбирает товары из того, что осталось после предыдущего.
    private void ApplyFilter()
    {
        var products = allProducts.AsEnumerable();

        // Фильтр 1. Категория (задание 2)
        var category = CategoryBox.SelectedItem as Category;
        if (category != null && category.Id != 0)
        {
            // Подсказка: Сейчас оно сравнивает Id ТОВАРА и Id категории. Очевидно, что они будут всегда разные.
            // У Product есть свойство CategoryId, которое можно использовать для этого. Как мы делали на дне 3?
            products = products.Where(p => p.CategoryId == category.Id);
        }

        // Фильтр 2. Поиск по названию (задание 3)
        var text = SearchBox.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            // Подсказка: Поиск по точному названию - это круто. Но что если пользователь напишет "кофе", а не "Кофе"?
            // Используй метод .ToLower() для имени продукта и вводимого текста
            products = products.Where(p => p.Name.ToLower().Contains(text.ToLower()));
        }

        // Фильтр 3. Только в наличии (задание 4) — напиши сам по образцу фильтров выше.
        // Подсказка: галочка стоит, если InStockBox.IsChecked == true
        if (InStockBox.IsChecked == true)
        {
            products = products.Where(p => p.Stock > 0);
        }

        var result = products.ToList();
        ProductsGrid.ItemsSource = result;

        // Задание 5. Покажи в FoundText, сколько товаров в таблице, например: «Найдено: 3»
        // Подсказка: Замени "НЕИЗВЕСТНО" на количество товаров в таблице. Используй свойство Count у result.
        FoundText.Text = $"Найдено: {result.Count}";
    }

    // ============ Часть B. Заказы ============

    // Загружаем все заказы в левую таблицу. Готово, ошибок нет
    private async Task LoadOrders()
    {
        OrdersGrid.ItemsSource = await Api.GetOrders();
    }

    // Задания 6–7. Пользователь выбрал заказ в левой таблице — показываем его состав справа
    private async void OrdersGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var order = OrdersGrid.SelectedItem as Order;
        if (order == null)
        {
            ItemsGrid.ItemsSource = null;
            TotalText.Text = "";
            return;
        }

        StatusBox.SelectedItem = order.Status;

        // Задание 6
        // Подсказка: Здесь мы получаем список товаров в заказе по ClientId, а нужно по Id odrer'а.
        var items = await Api.GetOrderItems(order.Id);
        ItemsGrid.ItemsSource = items;

        // Задание 7. Посчитай сумму заказа и покажи её в TotalText, например: «Сумма заказа: 24000 ₽»
var total = items.Sum(i => i.Price * i.Quantity);
TotalText.Text = $"Итого: {total:N2}";
    }

    // ============ Часть C. Изменение данных ============

    // Задание 8. Кнопка «Добавить» в форме «Новый товар»
    private async void AddProduct_Click(object? sender, RoutedEventArgs e)
    {
        var category = NewCategoryBox.SelectedItem as Category;
        if (category == null)
        {
            ShowMessage("Выбери категорию");
            return;
        }

        // int.TryParse пробует превратить текст в число.
        // Получилось — возвращает true, а число кладёт в price. Не получилось (например, «abc») — false
        if (!int.TryParse(PriceBox.Text, out int price))
        {
            ShowMessage("Цена должна быть числом");
            return;
        }

        // Задание 8. По аналогии с PriceBox выше, сделай проверку на число и запиши результат в stock. StockBox
        int stock = 0;

        var product = new Product
        {
            Name = NameBox.Text ?? "",
            CategoryId = category.Id,
            Price = price,
            Stock = stock,
        };

        // Остальные проверки (пустое название, цена больше нуля...) сделает API — оно пришлёт текст ошибки
        var error = await Api.AddProduct(product);
        if (error != null)
        {
            ShowMessage(error);
            return;
        }

        ShowMessage($"Товар «{product.Name}» добавлен");
        NameBox.Text = "";
        PriceBox.Text = "";
        StockBox.Text = "";

        // Задание 8. 
        // Подсказка: Добавь перезагрузку таблицы с помощью вызова LoadProducts();
    }

    // Задание 9. Кнопка «Сменить статус». Напиши сам — по образцу задания 8
    private async void SetStatus_Click(object? sender, RoutedEventArgs e)
    {

    }
}
