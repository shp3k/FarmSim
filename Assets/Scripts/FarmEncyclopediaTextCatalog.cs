public static class FarmEncyclopediaTextCatalog
{
    public static string GetCropDescription(string cropId)
    {
        return NormalizeId(cropId) switch
        {
            "wheat" => "Стартовая культура. Быстро растёт и подходит для первого заработка.",
            "carrot" => "Вторая культура. Даёт больше прибыли, но требует больше времени.",
            "potato" => "Ранняя стабильная культура для перехода к среднему этапу.",
            "corn" => "Более дорогая культура с увеличенной прибылью.",
            "beet" => "Культура среднего этапа, замедляет прогрессию и требует больше продаж.",
            "tomato" => "Средне-поздняя культура, важная для выхода к капусте.",
            "cabbage" => "Сильная культура перед престижной частью прогрессии.",
            "cucumber" => "Поздняя культура, требующая престиж.",
            "pumpkin" => "Эндгейм-культура, требующая несколько престижей.",
            "strawberry" => "Дорогая и прибыльная культура, доступная после серьёзного прогресса.",
            _ => "Описание пока не добавлено."
        };
    }

    public static string GetAnimalDescription(string animalId)
    {
        return NormalizeId(animalId) switch
        {
            "chicken" => "Первое животное. Приносит куриные яйца и даёт небольшой стабильный доход.",
            "goat" => "Приносит козье молоко и помогает перейти к среднему этапу животноводства.",
            "sheep" => "Приносит шерсть и нужна для открытия коровы.",
            "cow" => "Позднее животное. Приносит молоко и требует престиж.",
            "duck" => "Позднее животное. Приносит утиные яйца и требует несколько престижей.",
            _ => "Описание пока не добавлено."
        };
    }

    public static string GetProductDisplayName(string productItemId)
    {
        return NormalizeId(productItemId) switch
        {
            "egg" => "Яйца",
            "chicken_egg" => "Куриные яйца",
            "goat_milk" => "Козье молоко",
            "sheep_wool" => "Шерсть",
            "milk" => "Молоко",
            "cow_milk" => "Молоко",
            "duck_egg" => "Утиные яйца",
            _ => string.IsNullOrWhiteSpace(productItemId) ? "Продукция" : productItemId
        };
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
