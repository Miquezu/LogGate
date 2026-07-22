using LogGate.Interfaces;
using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LogGate.DataAccess
{
    internal class DataRepository: IDataRepository
    {
        // Метод для сохранения списка данных в базу
        public void SaveItems(IEnumerable<DataItem> items)
        {
            using (var context = new AppDBContext())
            {
                // Добавляем все элементы разом
                context.DataItems.AddRange(items);

                // Сохраняем изменения в файл базы данных
                context.SaveChanges();
            }
        }

        // Метод для выгрузки данных из базы (чтобы потом показать в таблице)
        public List<DataItem> GetAllItems()
        {
            using (var context = new AppDBContext())
            {
                // Возвращаем все записи как список
                return context.DataItems.ToList();
            }
        }
    }
}
