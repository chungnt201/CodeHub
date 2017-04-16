﻿using System;
using UIKit;
using ReactiveUI;
using CodeHub.iOS.TableViewSources;
using CoreGraphics;
using System.Reactive.Linq;
using CodeHub.iOS.Views;
using CodeHub.Core.ViewModels.Repositories;
using System.Reactive;

namespace CodeHub.iOS.ViewControllers.Repositories
{
	public class RepositoriesViewController : TableViewController
	{
		private readonly UISearchBar _repositorySearchBar = new UISearchBar(new CGRect(0, 0, 320, 44));
		private readonly LoadingIndicatorView _loading = new LoadingIndicatorView();

		private readonly Lazy<UIView> emptyView = new Lazy<UIView>((() =>
			new EmptyListView(Octicon.Repo.ToEmptyListImage(), "There are no repositories.")));

        public RepositoriesViewModel ViewModel { get; }

        public static RepositoriesViewController CreateMineViewController()
        {
            var viewModel = RepositoriesViewModel.CreateMineViewModel();
            return new RepositoriesViewController(viewModel);
        }

        public static RepositoriesViewController CreateUserViewController(string username)
        {
            var viewModel = RepositoriesViewModel.CreateUsersViewModel(username);
            return new RepositoriesViewController(viewModel);
        }

        public static RepositoriesViewController CreateStarredViewController()
        {
            var viewModel = RepositoriesViewModel.CreateStarredViewModel();
            return new RepositoriesViewController(viewModel) { Title = "Starred" };
        }

        public static RepositoriesViewController CreateWatchedViewController()
        {
            var viewModel = RepositoriesViewModel.CreateWatchedViewModel();
            return new RepositoriesViewController(viewModel) { Title = "Watched" };
        }

        public static RepositoriesViewController CreateForkedViewController(string username, string repository)
        {
            var viewModel = RepositoriesViewModel.CreateForkedViewModel(username, repository);
            return new RepositoriesViewController(viewModel) { Title = "Forks" };
        }

        public static RepositoriesViewController CreateOrganizationViewController(string org)
        {
            var viewModel = RepositoriesViewModel.CreateOrganizationViewModel(org);
            return new RepositoriesViewController(viewModel);
        }

        public RepositoriesViewController(RepositoriesViewModel viewModel)
			: base(UITableViewStyle.Plain)
		{
            ViewModel = viewModel;
            Title = "Repositories";
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

            var tableViewSource = new RepositoryTableViewSource(TableView, ViewModel.Items);
            TableView.Source = tableViewSource;

            Appearing
                .Take(1)
                .Select(_ => Unit.Default)
                .InvokeCommand(ViewModel.LoadCommand);

			this.WhenActivated(d =>
			{
				d(_repositorySearchBar.GetChangedObservable()
				  .Subscribe(x => ViewModel.SearchText = x));

                d(ViewModel.WhenAnyValue(x => x.IsEmpty)
                  .Skip(1)
                  .Subscribe(SetEmpty));

				d(ViewModel.RepositoryItemSelected
				  .Select(x => new RepositoryViewController(x.Owner, x.Name))
				  .Subscribe(x => NavigationController.PushViewController(x, true)));

                d(ViewModel.LoadCommand.IsExecuting
                  .Subscribe(Loading));

                d(ViewModel.LoadCommand
				  .Select(_ => ViewModel.LoadMoreCommand.IsExecuting)
                  .Switch()
                  .Throttle(TimeSpan.FromMilliseconds(150), RxApp.MainThreadScheduler)
                  .Subscribe(Loading));

                d(tableViewSource.RequestMore
                  .InvokeCommand(ViewModel.LoadMoreCommand));

                d(ViewModel.LoadCommand
                  .Select(_ => ViewModel.Items.Changed)
                  .Switch()
                  .Select(_ => Unit.Default)
                  .Throttle(TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler)
                  .Where(_ => TableView.LastItemVisible())
                  .InvokeCommand(ViewModel.LoadMoreCommand));

                d(ViewModel.LoadCommand.Merge(ViewModel.LoadMoreCommand)
                  .Throttle(TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler)
                  .Where(_ => TableView.LastItemVisible())
                  .InvokeCommand(ViewModel.LoadMoreCommand));
			});
		}

        private void SetEmpty(bool empty)
        {
            TableView.TableHeaderView = empty ? null : _repositorySearchBar;
            TableView.BackgroundView = empty ? emptyView.Value : null;
            TableView.SeparatorStyle = empty 
                ? UITableViewCellSeparatorStyle.None 
                : UITableViewCellSeparatorStyle.SingleLine;
        }

		private void Loading(bool searching)
		{
			_loading.SetLoading(searching);

			if (searching)
			{
				TableView.TableFooterView = _loading;
				TableView.BackgroundView = null;
			}
			else
			{
				TableView.TableFooterView = null;
			}
		}
	}
}
