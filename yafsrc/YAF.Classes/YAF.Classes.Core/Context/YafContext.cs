/* Yet Another Forum.NET
 * Copyright (C) 2006-2010 Jaben Cargman
 * http://www.yetanotherforum.net/
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 */
namespace YAF.Core
{
  #region Using

  using System;
  using System.Web;
  using System.Web.Profile;
  using System.Web.Security;

  using Autofac;

  using YAF.Classes;
  using YAF.Classes.Pattern;
  using YAF.Core.Services;
  using YAF.Types;
  using YAF.Types.Constants;
  using YAF.Types.Interfaces;
  using YAF.Utils;
  using YAF.Utils.Helpers;

  #endregion

  /// <summary>
  /// Context class that accessible with the same instance from all locations
  /// </summary>
  public class YafContext : UserPageBase, IDisposable, IHaveServiceLocator
  {
    #region Constants and Fields

    /// <summary>
    ///   The _application.
    /// </summary>
    protected static HttpApplicationState _application;

    /// <summary>
    /// The _context lifetime container.
    /// </summary>
    protected ILifetimeScope _contextLifetimeContainer;

    /// <summary>
    ///   The _current forum page.
    /// </summary>
    protected ForumPage _currentForumPage;

    /// <summary>
    ///   The _repository.
    /// </summary>
    protected ContextVariableRepository _repository;

    /// <summary>
    ///   The _single instance factory.
    /// </summary>
    protected SingleClassInstanceFactory _singleInstanceFactory = new SingleClassInstanceFactory();

    /// <summary>
    ///   The _variables.
    /// </summary>
    protected TypeDictionary _variables = new TypeDictionary();

    /// <summary>
    ///   The _user.
    /// </summary>
    private MembershipUser _user;

    #endregion

    #region Constructors and Destructors

    /// <summary>
    ///   Initializes a new instance of the <see cref = "YafContext" /> class. 
    ///   YafContext Constructor
    /// </summary>
    public YafContext()
    {
      this._contextLifetimeContainer = GlobalContainer.Container.BeginLifetimeScope(YafLifetimeScope.Context);

      // init the respository
      this._repository = new ContextVariableRepository(this._variables);

      // init context...
      if (this.Init != null)
      {
        this.Init(this, new EventArgs());
      }
    }

    #endregion

    #region Events

    /// <summary>
    ///   On YafContext Constructor Call
    /// </summary>
    public event EventHandler<EventArgs> Init;

    /// <summary>
    ///   On ForumPage Init Call
    /// </summary>
    public event EventHandler<EventArgs> PageInit;

    /// <summary>
    ///   On ForumPage Load Call
    /// </summary>
    public event EventHandler<EventArgs> PageLoad;

    /// <summary>
    ///   On ForumPage PreLoad (Before Load) Call
    /// </summary>
    public event EventHandler<EventArgs> PagePreLoad;

    /// <summary>
    ///   On ForumPage Unload Call
    /// </summary>
    public event EventHandler<EventArgs> PageUnload;

    /// <summary>
    ///   On YafContext Unload Call
    /// </summary>
    public event EventHandler<EventArgs> Unload;

    #endregion

    #region Properties

    /// <summary>
    ///   Get/set the current state of the Http Application.
    ///   Defaults to HttpContext.Current.Application. If not available
    ///   pulls from application variable.
    /// </summary>
    public static HttpApplicationState Application
    {
      get
      {
        if (HttpContext.Current != null)
        {
          return HttpContext.Current.Application;
        }

        return _application;
      }

      set
      {
        _application = value;
      }
    }

    /// <summary>
    ///   Get the instance of the Forum Context
    /// </summary>
    public static YafContext Current
    {
      get
      {
        return PageSingleton<YafContext>.Instance;
      }
    }

    /// <summary>
    ///   Current Page Instance of the Module Manager
    /// </summary>
    public YafBaseModuleManager BaseModuleManager
    {
      get
      {
        return this._singleInstanceFactory.GetInstance<YafBaseModuleManager>();
      }
    }

    /// <summary>
    ///   Current Board Settings
    /// </summary>
    public virtual YafBoardSettings BoardSettings
    {
      get
      {
        string key = YafCache.GetBoardCacheKey(Constants.Cache.BoardSettings);

        if (Application[key] == null)
        {
          Application[key] = new YafLoadBoardSettings(this.PageBoardID);
        }

        return (YafBoardSettings)Application[key];
      }

      set
      {
        string key = YafCache.GetBoardCacheKey(Constants.Cache.BoardSettings);

        if (value == null)
        {
          Application.Remove(key);
        }
        else
        {
          // set the updated board settings...	
          Application[key] = value;
        }
      }
    }

    /// <summary>
    ///   Current System-Wide Cache
    /// </summary>
    public YafCache Cache
    {
      get
      {
        return this._singleInstanceFactory.GetInstance<YafCache>();
      }
    }

    /// <summary>
    ///   Forum page instance of the current forum page.
    ///   May not be valid until everything is initialized.
    /// </summary>
    public ForumPage CurrentForumPage
    {
      get
      {
        return this._currentForumPage;
      }

      set
      {
        this._currentForumPage = value;
        value.Load += this.ForumPageLoad;
        value.Unload += this.ForumPageUnload;
      }
    }

    /// <summary>
    ///   Current Membership Provider used by YAF
    /// </summary>
    public MembershipProvider CurrentMembership
    {
      get
      {
        if (Config.MembershipProvider.IsSet() && Membership.Providers[Config.MembershipProvider] != null)
        {
          return Membership.Providers[Config.MembershipProvider];
        }

        // return default membership provider
        return Membership.Provider;
      }
    }

    /// <summary>
    ///   Current Profile Provider used by YAF
    /// </summary>
    public ProfileProvider CurrentProfile
    {
      get
      {
        if (Config.ProviderProvider.IsSet() && ProfileManager.Providers[Config.ProviderProvider] != null)
        {
          return ProfileManager.Providers[Config.ProviderProvider];
        }

        // return default membership provider
        return ProfileManager.Provider;
      }
    }

    /// <summary>
    ///   Current Membership Roles Provider used by YAF
    /// </summary>
    public RoleProvider CurrentRoles
    {
      get
      {
        if (Config.RoleProvider.IsSet() && Roles.Providers[Config.RoleProvider] != null)
        {
          return Roles.Providers[Config.RoleProvider];
        }

        // return default role provider
        return Roles.Provider;
      }
    }

    /// <summary>
    ///   Instance of the Combined UserData for the current user.
    /// </summary>
    public CombinedUserDataHelper CurrentUserData
    {
      get
      {
        return this._singleInstanceFactory.GetInstance<CombinedUserDataHelper>();
      }
    }

    /// <summary>
    ///   Current Page Instance of the Module Manager
    /// </summary>
    [Obsolete("Use Service Location or Dependency Injection to get interface: IModuleManager<ForumEditor>")]
    public IModuleManager<ForumEditor> EditorModuleManager
    {
      get
      {
        return this.Get<IModuleManager<ForumEditor>>();
      }
    }

    /// <summary>
    ///   Get the current page as the forumPage Enum (for comparison)
    /// </summary>
    public ForumPages ForumPageType
    {
      get
      {
        if (this.Get<HttpRequestBase>().QueryString.GetFirstOrDefault("g").IsNotSet())
        {
          return ForumPages.forum;
        }

        try
        {
          return this.Get<HttpRequestBase>().QueryString.GetFirstOrDefault("g").ToEnum<ForumPages>(true);
        }
        catch (Exception)
        {
          return ForumPages.forum;
        }
      }
    }

    /// <summary>
    ///   Access to the Context Global Variable Repository Class
    ///   which is a helper class that accesses YafContext.Vars with strongly
    ///   typed properties for primary variables.
    /// </summary>
    public ContextVariableRepository Globals
    {
      get
      {
        return this._repository;
      }
    }

    /// <summary>
    ///   Current Page Load Message
    /// </summary>
    public LoadMessage LoadMessage
    {
      get
      {
        return this._singleInstanceFactory.GetInstance<LoadMessage>();
      }
    }

    /// <summary>
    /// Gets Localization.
    /// </summary>
    [Obsolete("Use Service Location or Dependency Injection to get interface: ILocalization")]
    public ILocalization Localization
    {
      get
      {
        return this.Get<ILocalization>();
      }
    }

    /// <summary>
    ///   Current Page Elements
    /// </summary>
    public PageElementRegister PageElements
    {
      get
      {
        return this._singleInstanceFactory.GetInstance<PageElementRegister>();
      }
    }

    /// <summary>
    ///   Current Page User Profile
    /// </summary>
    public YafUserProfile Profile
    {
      get
      {
        return (YafUserProfile)HttpContext.Current.Profile;
      }
    }

    /// <summary>
    ///   Current Page Query ID Helper
    /// </summary>
    public QueryStringIDHelper QueryIDs
    {
      get
      {
        return this._singleInstanceFactory.GetInstance<QueryStringIDHelper>();
      }

      set
      {
        this._singleInstanceFactory.SetInstance(value);
      }
    }

    /// <summary>
    ///   Provides access to the Service Locatorer
    /// </summary>
    public IServiceLocator ServiceLocator
    {
      get
      {
        return this._contextLifetimeContainer.Resolve<IServiceLocator>();
      }
    }

    /// <summary>
    ///   Current Page Control Settings from Forum Control
    /// </summary>
    public YafControlSettings Settings
    {
      get
      {
        return YafControlSettings.Current;
      }
    }

    /// <summary>
    /// Gets Theme.
    /// </summary>
    [Obsolete("Use Service Location or Dependency Injection to get interface: ITheme")]
    public ITheme Theme
    {
      get
      {
        return this.Get<ITheme>();
      }
    }

    /// <summary>
    ///   Gets the UrlBuilder
    /// </summary>
    public IUrlBuilder UrlBuilder
    {
      get
      {
        return YafFactoryProvider.UrlBuilder;
      }
    }

    /// <summary>
    ///   Current Membership User
    /// </summary>
    public MembershipUser User
    {
      get
      {
        if (this._user == null)
        {
          this._user = UserMembershipHelper.GetUser();
        }

        return this._user;
      }

      set
      {
        this._user = value;
      }
    }

    /// <summary>
    ///   YafContext Global Instance Variables
    ///   Use for plugins or other situations where a value is needed per instance.
    /// </summary>
    public TypeDictionary Vars
    {
      get
      {
        return this._variables;
      }
    }

    #endregion

    #region Indexers

    /// <summary>
    ///   Returns a value from the YafContext Global Instance Variables (Vars) collection.
    /// </summary>
    /// <param name = "varName"></param>
    /// <returns>Value if it's found, null if it doesn't exist.</returns>
    public object this[[NotNull] string varName]
    {
      get
      {
        if (this._variables.ContainsKey(varName))
        {
          return this._variables[varName];
        }

        return null;
      }

      set
      {
        this._variables[varName] = value;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Helper Function that adds a "load message" to the load message class.
    /// </summary>
    /// <param name="loadMessage">
    /// </param>
    public void AddLoadMessage([NotNull] string loadMessage)
    {
      this.LoadMessage.Add(loadMessage);
    }

    #endregion

    #region Implemented Interfaces

    #region IDisposable

    /// <summary>
    /// The dispose.
    /// </summary>
    public void Dispose()
    {
      if (this.Unload != null)
      {
        this.Unload(this, new EventArgs());
      }

      this._contextLifetimeContainer.Dispose();
    }

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Fired from ForumPage
    /// </summary>
    /// <param name="sender">
    /// </param>
    /// <param name="e">
    /// </param>
    internal void ForumPageInit([NotNull] object sender, [NotNull] EventArgs e)
    {
      if (this.PageInit != null)
      {
        this.PageInit(this, new EventArgs());
      }
    }

    /// <summary>
    /// Fired from ForumPage
    /// </summary>
    /// <param name="sender">
    /// </param>
    /// <param name="e">
    /// </param>
    internal void ForumPagePreLoad([NotNull] object sender, [NotNull] EventArgs e)
    {
      if (this.PagePreLoad != null)
      {
        this.PagePreLoad(this, new EventArgs());
      }
    }

    /// <summary>
    /// The forum page load.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The e.
    /// </param>
    protected void ForumPageLoad([NotNull] object sender, [NotNull] EventArgs e)
    {
      if (this.PageLoad != null)
      {
        this.PageLoad(this, new EventArgs());
      }
    }

    /// <summary>
    /// The forum page unload.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The e.
    /// </param>
    protected void ForumPageUnload([NotNull] object sender, [NotNull] EventArgs e)
    {
      if (this.PageUnload != null)
      {
        this.PageUnload(this, new EventArgs());
      }
    }

    #endregion
  }
}