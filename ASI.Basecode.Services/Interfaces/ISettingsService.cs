using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ISettingsService
    {
        SettingsIndexViewModel GetSettings();

        (bool Success, string Error) SavePolicy(PolicyFormViewModel form);

        (bool Success, string Error) AddCategory(string code, string name);

        (bool Success, string Error) DeleteCategory(long categoryId);

        (bool Success, string Error) AddLocation(string code, string name, bool isPickup);

        (bool Success, string Error) DeleteLocation(long locationId);

        NotificationsIndexViewModel GetNotifications(long userId);

        string OpenNotification(long notificationId, long userId);

        void MarkNotificationRead(long notificationId, long userId);

        void MarkAllNotificationsRead(long userId);
    }
}
