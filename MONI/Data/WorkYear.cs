﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using MONI.Data.SpecialDays;
using MONI.Util;
using MONI.ViewModels;

namespace MONI.Data
{
    public class WorkYear : ViewModelBase
    {
        private readonly int hitListLookBackInWeeks;
        private readonly PNSearchViewModel pnSearch;
        private readonly PositionSearchViewModel positionSearch;

        public int Year { get; set; }

        public WorkYear(int year, WorkDayParserSettings parserSettings, int hitListLookBackInWeeks, float hoursPerDay, PNSearchViewModel pnSearch, PositionSearchViewModel positionSearch)
        {
            this.hitListLookBackInWeeks = hitListLookBackInWeeks;
            this.pnSearch = pnSearch;
            this.positionSearch = positionSearch;
            this.Year = year;
            this.Months = new ObservableCollection<WorkMonth>();
            this.Weeks = new ObservableCollection<WorkWeek>();

            var germanSpecialDays = SpecialDaysUtils.GetGermanSpecialDays(year);

            var cal = new GregorianCalendar();
            for (int month = 1; month <= cal.GetMonthsInYear(year); month++)
            {
                WorkMonth wm = new WorkMonth(year, month, germanSpecialDays, parserSettings, hoursPerDay);
                this.Months.Add(wm);
                foreach (var workWeek in wm.Weeks)
                {
                    this.Weeks.Add(workWeek);
                    workWeek.PropertyChanged += this.workWeek_PropertyChanged;
                }
            }
            this.ProjectHitlist = new QuickFillObservableCollection<HitlistInfo>();
            this.PositionHitlist = new QuickFillObservableCollection<HitlistInfo>();

            Dispatcher.CurrentDispatcher.InvokeAsync(() => this.UpdateProjectHitlistAsync().ConfigureAwait(false), DispatcherPriority.ApplicationIdle);
            Dispatcher.CurrentDispatcher.InvokeAsync(() => this.UpdatePositionHitlistAsync().ConfigureAwait(false), DispatcherPriority.ApplicationIdle);
        }

        private async void workWeek_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // this property doesn't exist here, but we want to trigger saving the data at MainViewModel
            this.OnPropertyChanged("HoursDuration");
            await this.UpdateProjectHitlistAsync().ConfigureAwait(false);
            await this.UpdatePositionHitlistAsync().ConfigureAwait(false);
        }

        public ObservableCollection<WorkMonth> Months { get; protected set; }
        public ObservableCollection<WorkWeek> Weeks { get; protected set; }
        public QuickFillObservableCollection<HitlistInfo> ProjectHitlist { get; protected set; }
        public QuickFillObservableCollection<HitlistInfo> PositionHitlist { get; protected set; }

        public async Task UpdateProjectHitlistAsync()
        {
            var newHitlist = await GetProjectHitlistAsync(this.Months, this.hitListLookBackInWeeks, this.pnSearch).ConfigureAwait(false);
            this.ProjectHitlist.AddItems(newHitlist, true);
        }

        private static async Task<IEnumerable<HitlistInfo>> GetProjectHitlistAsync(IEnumerable<WorkMonth> months, int lookBackInWeeks, PNSearchViewModel pnSearchViewModel)
        {
            return await Task.Factory.StartNew(() =>
            {
                var allDays = months.SelectMany(m => m.Days);
                var daysFromLookback = lookBackInWeeks > 0 ? allDays.Where(m => m.DateTime > DateTime.Now.AddDays(lookBackInWeeks * -7)) : allDays;
                var hitlistInfos = daysFromLookback
                    .SelectMany(d => d.Items)
                    .GroupBy(p => p.Project)
                    .Select(g =>
                        new HitlistInfo(
                            g.Key,
                            g.Count(),
                            g.Sum(wi => wi.HoursDuration),
                            g.OrderByDescending(p => p.WorkDay.DateTime)
                                .Select(p => pnSearchViewModel.GetDescriptionForProjectNumber(p.Project))
                                .FirstOrDefault())
                    );
                return hitlistInfos.OrderByDescending(g => g.HoursUsed);
            }).ConfigureAwait(false);
        }

        public async Task UpdatePositionHitlistAsync()
        {
            var newHitlist = await GetPositionHitlistAsync(this.Months, this.hitListLookBackInWeeks, this.positionSearch).ConfigureAwait(false);
            this.PositionHitlist.AddItems(newHitlist, true);
        }

        private static async Task<IEnumerable<HitlistInfo>> GetPositionHitlistAsync(IEnumerable<WorkMonth> months, int lookBackInWeeks, PositionSearchViewModel posSearchViewModel)
        {
            return await Task.Factory.StartNew(() =>
            {
                if (posSearchViewModel != null)
                {
                    var allDays = months.SelectMany(m => m.Days);
                    var daysFromLookback = lookBackInWeeks > 0 ? allDays.Where(m => m.DateTime > DateTime.Now.AddDays(lookBackInWeeks * -7)) : allDays;
                    var hitlistInfos = daysFromLookback
                        .SelectMany(d => d.Items)
                        .GroupBy(p => p.Position)
                        .Select(g =>
                            new HitlistInfo(
                                g.Key,
                                g.Count(),
                                g.Sum(wi => wi.HoursDuration),
                                posSearchViewModel.GetDescriptionForPositionNumber(g.Key))
                        );
                    return hitlistInfos.OrderByDescending(g => g.HoursUsed);
                }
                else
                {
                    return Enumerable.Empty<HitlistInfo>();
                }
            }).ConfigureAwait(false);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1} months", this.Year, this.Months.Count);
        }

        public WorkDay GetDay(int month, int day)
        {
            return this.Months.ElementAt(month - 1).Days.ElementAt(day - 1);
        }
    }

    public class HitlistInfo
    {
        public double HoursUsed { get; set; }
        public string Key { get; private set; }
        public int Count { get; private set; }
        public string LastUsedDescription { get; private set; }

        public HitlistInfo(string key, int count, double hoursUsed, string lastUsedDescription)
        {
            this.HoursUsed = hoursUsed;
            this.Key = key;
            this.Count = count;
            this.LastUsedDescription = lastUsedDescription;
        }
    }
}