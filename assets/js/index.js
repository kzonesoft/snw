﻿document.addEventListener('DOMContentLoaded', () => {
    const submenuLinks = document.querySelectorAll('.submenu-item > a');
    const mainContent = document.getElementById('main-content');
    const cache = {}; // Bộ nhớ cache để lưu nội dung các trang
    let currentPage = null; // Biến lưu trữ trang hiện tại đang hiển thị
    let activeUpdateFunction = null; // Lưu trữ hàm cập nhật đang hoạt động

    // Tính toán lại kích thước cho content margin
    function adjustContentMargin() {
        if (window.innerWidth <= 767) {
            const sidebarHeight = Math.min(
                document.querySelector('.sidebar').scrollHeight,
                window.innerHeight * 0.5
            );
            document.querySelector('.content').style.marginTop = (sidebarHeight + 10) + 'px';
            console.log('Adjusted content margin to:', (sidebarHeight + 10) + 'px');
        } else {
            document.querySelector('.content').style.marginTop = '0';
        }
    }

    // Xử lý hiển thị submenu trên mobile
    function setupMobileMenu() {
        if (window.innerWidth > 767) return; // Chỉ áp dụng cho mobile

        // Xóa các event listener cũ
        document.querySelectorAll('.menu-item > label').forEach(label => {
            const clone = label.cloneNode(true);
            label.parentNode.replaceChild(clone, label);
        });

        // Thêm event listener mới
        document.querySelectorAll('.menu-item > label').forEach(label => {
            label.addEventListener('click', function (e) {
                console.log('Menu label clicked');
                e.preventDefault();
                e.stopPropagation();

                const menuItem = this.parentNode;
                const isOpen = menuItem.classList.contains('open');

                // Đóng tất cả các menu khác
                document.querySelectorAll('.menu-item').forEach(item => {
                    if (item !== menuItem) {
                        item.classList.remove('open');
                    }
                });

                // Mở/đóng menu hiện tại
                menuItem.classList.toggle('open');

                // Cuộn để đảm bảo submenu hiển thị đầy đủ
                if (menuItem.classList.contains('open')) {
                    setTimeout(() => {
                        const submenu = menuItem.querySelector('.submenu');
                        const menuRect = menuItem.getBoundingClientRect();

                        // Kiểm tra xem submenu có bị che khuất không
                        if (menuRect.bottom + submenu.scrollHeight > window.innerHeight) {
                            menuItem.scrollIntoView({ behavior: 'smooth', block: 'start' });
                        }
                    }, 100);
                }

                // Tính lại margin cho content
                setTimeout(adjustContentMargin, 150);
            });
        });

        // Xử lý click trên submenu item để đóng menu sau khi chọn
        document.querySelectorAll('.submenu-item > a').forEach(item => {
            item.addEventListener('click', function () {
                // Đóng tất cả menu sau khi chọn
                setTimeout(() => {
                    document.querySelectorAll('.menu-item').forEach(menuItem => {
                        menuItem.classList.remove('open');
                    });
                    adjustContentMargin();
                }, 300); // Delay để người dùng thấy được họ đã click vào item
            });
        });

        // Đóng menu khi click ra ngoài
        document.addEventListener('click', function (e) {
            if (!e.target.closest('.menu-item')) {
                document.querySelectorAll('.menu-item').forEach(item => {
                    item.classList.remove('open');
                });
                adjustContentMargin();
            }
        });
    }

    // Gọi hàm để thiết lập menu mobile
    setupMobileMenu();

    // Xử lý khi resize
    window.addEventListener('resize', function () {
        setupMobileMenu();
        adjustContentMargin();
        limitSidebarHeight();
    });

    // Đảm bảo sidebar không quá cao
    function limitSidebarHeight() {
        if (window.innerWidth <= 767) {
            const sidebar = document.querySelector('.sidebar');
            sidebar.style.maxHeight = '50vh';
        } else {
            const sidebar = document.querySelector('.sidebar');
            sidebar.style.maxHeight = '100vh';
        }
    }

    // Gọi hàm giới hạn chiều cao sidebar
    limitSidebarHeight();

    function loadPage(pageUrl) {
        // Lưu URL trang hiện tại (điều này quan trọng để kiểm tra cache hoạt động)
        console.log(`Yêu cầu tải trang: ${pageUrl}, trang hiện tại: ${currentPage}`);

        // Kiểm tra nếu đang ở trang hiện tại
        if (pageUrl === currentPage) {
            console.log(`CACHE: Trang ${pageUrl} đã được hiển thị. Không tải lại.`);
            return; // Thoát hàm, không làm gì cả
        }

        // Dừng các hàm cập nhật tự động nếu có
        stopCurrentUpdates();

        // Cập nhật trang hiện tại
        currentPage = pageUrl;

        // Kiểm tra cache
        if (cache[pageUrl]) {
            console.log(`CACHE: Sử dụng cache cho trang ${pageUrl}`);
            mainContent.innerHTML = cache[pageUrl];
            initPageScripts();
            return;
        }

        // Nếu không có trong cache, hiển thị thông báo đang tải
        console.log(`CACHE: Trang ${pageUrl} không có trong cache, đang tải từ server`);
        mainContent.innerHTML = '<p>Đang tải...</p>';

        const token = sessionStorage.getItem('token');
        fetch(pageUrl, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
            }
        })
            .then(response => {
                if (response.status === 401) {
                    console.warn('Người dùng chưa được xác thực. Điều hướng đến trang đăng nhập.');
                    sessionStorage.removeItem('token');
                    window.location.href = '/';
                    return Promise.reject(new Error('401 Unauthorized'));
                }
                if (!response.ok) {
                    throw new Error(`HTTP error! Status: ${response.status}`);
                }
                return response.text();
            })
            .then(data => {
                // Lưu vào cache
                console.log(`CACHE: Lưu trang ${pageUrl} vào cache`);
                cache[pageUrl] = data;

                // Hiển thị nội dung
                if (currentPage === pageUrl) {
                    mainContent.innerHTML = data;
                    initPageScripts();
                } else {
                    console.log(`CACHE: Trang đã thay đổi trong khi tải, không hiển thị ${pageUrl}`);
                }
            })
            .catch(error => {
                if (error.message !== '401 Unauthorized') {
                    console.error('Lỗi khi tải trang:', error);
                    mainContent.innerHTML = `<p style="color: red;">Lỗi khi tải trang: ${error.message}</p>`;
                }
            });
    }

    // Dừng các hàm cập nhật tự động của trang trước khi chuyển trang
    function stopCurrentUpdates() {
        // Dùng window.stopAutoUpdate nếu có
        if (typeof window.stopAutoUpdate === 'function') {
            console.log('Dừng cập nhật tự động trước khi chuyển trang');
            try {
                window.stopAutoUpdate();
            } catch (error) {
                console.error('Lỗi khi dừng cập nhật:', error);
            }
        }
    }

    function initPageScripts() {
        console.log('Khởi động lại script từ nội dung mới.');

        // Reset các hàm auto update
        let lastStartAutoUpdate = window.startAutoUpdate;
        let lastStopAutoUpdate = window.stopAutoUpdate;
        window.startAutoUpdate = null;
        window.stopAutoUpdate = null;

        // Đảm bảo dừng cập nhật từ trang trước nếu có
        if (typeof lastStopAutoUpdate === 'function') {
            console.log('Đảm bảo dừng cập nhật từ trang trước');
            try {
                lastStopAutoUpdate();
            } catch (error) {
                console.error('Lỗi khi dừng cập nhật từ trang trước:', error);
            }
        }

        // Tạo và thực thi các script
        const scripts = mainContent.querySelectorAll('script');
        for (const script of scripts) {
            const newScript = document.createElement('script');
            if (script.src) {
                newScript.src = script.src;
            } else {
                newScript.textContent = script.textContent;
            }
            document.head.appendChild(newScript);
        }

        // Khởi động trang hiện tại ngay sau khi thêm script
        setTimeout(() => startCurrentPage(), 100);
    }

    function startCurrentPage() {
        if (typeof window.startAutoUpdate === 'function') {
            console.log(`Gọi hàm startAutoUpdate cho trang ${currentPage}`);
            window.startAutoUpdate();
        } else {
            console.warn(`Hàm startAutoUpdate không được định nghĩa cho trang ${currentPage}`);
        }
    }

    // Xử lý sự kiện click trên các submenu
    document.addEventListener('click', function (e) {
        // Kiểm tra xem có phải click vào submenu-item > a không
        if (e.target.matches('.submenu-item > a')) {
            e.preventDefault();

            // Đóng menu trên mobile khi click vào submenu item
            if (window.innerWidth <= 767) {
                setTimeout(() => {
                    document.querySelectorAll('.menu-item').forEach(item => {
                        item.classList.remove('open');
                    });
                    adjustContentMargin();
                }, 300);
            }

            // Xóa selected từ tất cả các liên kết
            document.querySelectorAll('.submenu-item > a.selected').forEach(link => {
                link.classList.remove('selected');
            });

            // Thêm selected vào liên kết được click
            e.target.classList.add('selected');

            // Lấy URL trang từ thuộc tính data-page
            const pageUrl = e.target.getAttribute('data-page');

            // Cập nhật URL và tải trang
            window.history.pushState({ pageUrl }, '', `#${pageUrl}`);
            loadPage(pageUrl);
        }
    });

    function handleRefresh() {
        // Lấy pageUrl từ hash URL hoặc mặc định là /wks
        const pageUrl = window.location.hash.replace('#', '') || '/wks';
        console.log(`Khởi động với trang: ${pageUrl}`);

        // Tìm và đánh dấu liên kết active
        const activeLink = [...submenuLinks].find(link => link.getAttribute('data-page') === pageUrl);
        if (activeLink) {
            activeLink.classList.add('selected');
            loadPage(pageUrl);
        } else {
            console.warn(`Không tìm thấy liên kết phù hợp với hash: ${pageUrl}`);
            loadPage('/wks'); // Mặc định tải trang /wks
        }
    }

    // Xử lý nút back/forward của trình duyệt
    window.addEventListener('popstate', (event) => {
        const pageUrl = event.state?.pageUrl || '/wks';
        loadPage(pageUrl);
    });

    // Sự kiện khi người dùng chuẩn bị rời khỏi trang
    window.addEventListener('beforeunload', () => {
        stopCurrentUpdates();
    });

    // Khởi tạo trang ban đầu
    handleRefresh();

    // Xử lý nút logout
    const logoutButton = document.getElementById('logout-button');
    if (logoutButton) {
        logoutButton.addEventListener('click', () => {
            sessionStorage.removeItem('token');
            window.location.href = '/';
        });
    } else {
        console.warn('Không tìm thấy nút logout.');
    }

    // Điều chỉnh margin ban đầu
    adjustContentMargin();

    // Xử lý sự kiện khi trang hoàn tất tải
    window.addEventListener('load', function () {
        adjustContentMargin();
        setupMobileMenu();
    });
});