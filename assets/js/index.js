document.addEventListener('DOMContentLoaded', () => {
    const submenuLinks = document.querySelectorAll('.submenu-item > a');
    const mainContent = document.getElementById('main-content');
    const cache = {}; // Bộ nhớ cache để lưu nội dung các trang
    let currentPage = null; // Biến lưu trữ trang hiện tại đang hiển thị

    function loadPage(pageUrl) {
        if (!pageUrl || currentPage === pageUrl) {
            console.log(`Trang ${pageUrl} đã được hiển thị.`);
            return;
        }

        currentPage = pageUrl;
        mainContent.innerHTML = '<p>Đang tải...</p>'; // Hiển thị trạng thái tải

        if (cache[pageUrl]) {
            console.log(`Loading ${pageUrl} from cache`);
            mainContent.innerHTML = cache[pageUrl];
            initPageScripts();
            return;
        }
        const token = sessionStorage.getItem('token'); // Lấy token từ localStorage
        fetch(pageUrl, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`, // Sử dụng backtick để chèn biến token
            }
        })
            .then(response => {
                if (response.status === 401) {
                    console.warn('Người dùng chưa được xác thực. Điều hướng đến trang đăng nhập.');
                    // Xóa token (nếu cần) và điều hướng đến /login
                    localStorage.removeItem('token');
                    //window.location.href = '/';
                    return Promise.reject(new Error('401 Unauthorized'));
                }
                if (!response.ok) {
                    throw new Error(`HTTP error! Status: ${response.status}`);
                }
                return response.text();
            })
            .then(data => {
                cache[pageUrl] = data;
                mainContent.innerHTML = data;
                initPageScripts();
            })
            .catch(error => {
                if (error.message !== '401 Unauthorized') {
                    mainContent.innerHTML = `<p style="color: red;">Lỗi khi tải trang: ${error.message}</p>`;
                    currentPage = null;
                }
            });
    }


    function initPageScripts() {
        console.log('Khởi động lại script từ nội dung mới.');

        const scripts = mainContent.querySelectorAll('script');
        scripts.forEach(script => {
            const newScript = document.createElement('script');
            newScript.async = true; // Đảm bảo tải script không đồng bộ
            if (script.src) {
                newScript.src = script.src;
            } else {
                newScript.innerHTML = script.innerHTML;
            }
            document.body.appendChild(newScript);
            document.body.removeChild(newScript);
        });

        if (typeof startAutoUpdate === 'function') {
            console.log('Gọi lại hàm startAutoUpdate.');
            startAutoUpdate();
        } else {
            console.warn('Hàm startAutoUpdate không được định nghĩa.');
        }
    }

    // Xử lý sự kiện click trên các submenu
    document.querySelector('.submenu').addEventListener('click', function (e) {
        if (e.target.matches('.submenu-item > a')) {
            e.preventDefault();
            document.querySelector('.submenu-item > a.selected')?.classList.remove('selected');
            e.target.classList.add('selected');
            const pageUrl = e.target.getAttribute('data-page');
            window.history.pushState({ pageUrl }, '', `#${pageUrl}`);
            loadPage(pageUrl);
        }
    });

    function handleRefresh() {
        const pageUrl = window.location.hash.replace('#', '') || '/wks';
        const activeLink = [...submenuLinks].find(link => link.getAttribute('data-page') === pageUrl);
        if (activeLink) {
            activeLink.classList.add('selected');
            loadPage(pageUrl);
        } else {
            console.warn('Không tìm thấy liên kết phù hợp với hash.');
        }
    }

    window.addEventListener('popstate', (event) => {
        const pageUrl = event.state?.pageUrl || '/wks';
        if (pageUrl !== currentPage) {
            loadPage(pageUrl);
        }
    });

    handleRefresh();

    // Xử lý logic "Logout"
    const logoutButton = document.getElementById('logout-button');
    if (logoutButton) {
        logoutButton.addEventListener('click', () => {
            localStorage.removeItem('token'); // Xóa token khỏi localStorage
            window.location.href = '/'; // Điều hướng về trang đăng nhập
        });
    } else {
        console.warn('Không tìm thấy nút logout.');
    }
});
