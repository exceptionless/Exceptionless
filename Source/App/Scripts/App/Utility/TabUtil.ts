/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class TabUtil {
        public static init(tabElementId: string) {
            if (StringUtil.isNullOrEmpty(tabElementId))
                return;

            var activeTab = $(StringUtil.format('#{id} a[href="{hash}"]', { id: tabElementId, hash: location.hash.split('?')[0] }));
            if (activeTab.length > 0)
                activeTab.tab('show');

            $(StringUtil.format('#{id} a', { id: tabElementId })).on('click', function (e) {
                // NOTE: data-toggle="tab" will call e.preventDefault and show the tab, but it will not update the location.hash.
                e.preventDefault();
                location.hash = (<any>e.target).hash;
            })

            window.addEventListener('hashchange', (hashChangeEvent: any) => $('a[href="' + location.hash + '"]').tab('show'));
        }
    }
}