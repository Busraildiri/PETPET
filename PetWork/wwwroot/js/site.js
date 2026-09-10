// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

/* PetWork Custom JavaScript */

// Wait for the DOM to be fully loaded
document.addEventListener('DOMContentLoaded', function () {
    
    // Enable Bootstrap tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl)
    });
    
    // Enable Bootstrap popovers
    var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'))
    var popoverList = popoverTriggerList.map(function (popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl)
    });
    
    // Add active class to current navbar item
    const currentLocation = window.location.pathname;
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link');
    
    navLinks.forEach(link => {
        const linkPath = link.getAttribute('href');
        
        if (linkPath && currentLocation.includes(linkPath) && linkPath !== '/') {
            link.classList.add('active');
            
            // If it's in a dropdown, also highlight the dropdown
            const dropdownParent = link.closest('.nav-item.dropdown');
            if (dropdownParent) {
                const dropdownToggle = dropdownParent.querySelector('.dropdown-toggle');
                if (dropdownToggle) {
                    dropdownToggle.classList.add('active');
                }
            }
        } else if (linkPath === '/' && currentLocation === '/') {
            link.classList.add('active');
        }
    });
    
    // Animated scroll to top button
    const scrollToTopBtn = document.getElementById('scrollToTop');
    
    if (scrollToTopBtn) {
        window.addEventListener('scroll', function() {
            if (window.pageYOffset > 300) {
                scrollToTopBtn.classList.add('visible');
            } else {
                scrollToTopBtn.classList.remove('visible');
            }
        });
        
        scrollToTopBtn.addEventListener('click', function() {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });
    }
    
    // Confirmation modals
    const confirmationModals = document.querySelectorAll('[data-confirm]');
    
    confirmationModals.forEach(element => {
        element.addEventListener('click', function(e) {
            if (!confirm(this.getAttribute('data-confirm'))) {
                e.preventDefault();
            }
        });
    });
    
    // Character counter for textareas
    const textareas = document.querySelectorAll('textarea[maxlength]');
    
    textareas.forEach(textarea => {
        const maxLength = textarea.getAttribute('maxlength');
        const counter = document.createElement('div');
        counter.className = 'form-text text-end';
        const counterValue = document.createElement('span');
        counterValue.textContent = '0';
        counter.append(counterValue, `/${maxLength} karakter`);
        
        textarea.parentNode.appendChild(counter);
        
        textarea.addEventListener('input', function() {
            const currentLength = this.value.length;
            counterValue.textContent = currentLength;
            
            if (currentLength > maxLength * 0.9) {
                counter.classList.add('text-danger');
            } else {
                counter.classList.remove('text-danger');
            }
        });
    });
});
